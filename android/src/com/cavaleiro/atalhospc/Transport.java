package com.cavaleiro.atalhospc;

import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothSocket;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.Closeable;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.InterfaceAddress;
import java.net.NetworkInterface;
import java.net.Socket;
import java.net.SocketException;
import java.net.SocketTimeoutException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.UUID;

/** Abre a conexao pelo meio escolhido. Destinos: "tcp:host:porta" ou "bt:AA:BB:CC:DD:EE:FF". */
final class Transport implements Closeable {
    static final int DEFAULT_PORT = 47810;
    // Mesmo UUID em windows/src/Bluetooth.cs
    private static final UUID SERVICE = UUID.fromString("7a3f1c52-8e44-4d6b-9b21-5f0c7e1a2d90");
    private static final String ASK = "ATALHOSPC?1";

    final InputStream in;
    final OutputStream out;
    final String label;
    final String target;
    private final Closeable closer;
    private final Socket tcp;

    private Transport(InputStream in, OutputStream out, Closeable closer, Socket tcp, String label, String target) {
        this.in = in;
        this.out = out;
        this.closer = closer;
        this.tcp = tcp;
        this.label = label;
        this.target = target;
    }

    static Transport open(String target) throws IOException {
        if (target.startsWith("bt:")) return openBluetooth(target);
        int colon = target.lastIndexOf(':');
        String host = target.substring(4, colon);
        int port = Integer.parseInt(target.substring(colon + 1));
        Socket s = new Socket();
        try {
            s.setTcpNoDelay(true);
            s.connect(new InetSocketAddress(host, port), 1800);
            s.setSoTimeout(15000);
            return new Transport(s.getInputStream(), s.getOutputStream(), s, s, labelFor(s), target);
        } catch (IOException e) {
            s.close();
            throw e;
        }
    }

    private static Transport openBluetooth(String target) throws IOException {
        BluetoothAdapter adapter = BluetoothAdapter.getDefaultAdapter();
        if (adapter == null) throw new IOException("Este aparelho não tem Bluetooth.");
        try {
            if (!adapter.isEnabled()) throw new IOException("O Bluetooth está desligado neste aparelho.");
            BluetoothSocket s = adapter.getRemoteDevice(target.substring(3)).createRfcommSocketToServiceRecord(SERVICE);
            adapter.cancelDiscovery(); // uma busca em andamento atrapalha a conexao
            try {
                s.connect();
                return new Transport(s.getInputStream(), s.getOutputStream(), s, null, "Bluetooth", target);
            } catch (IOException e) {
                s.close();
                throw e;
            }
        } catch (SecurityException e) {
            throw new IOException("Falta a permissão de Bluetooth.");
        } catch (IllegalArgumentException e) {
            throw new IOException("Endereço Bluetooth inválido.");
        }
    }

    void setReadTimeout(int ms) {
        if (tcp == null) return; // no Bluetooth quem controla o tempo e o vigia do Link
        try {
            tcp.setSoTimeout(ms);
        } catch (SocketException ignored) {
        }
    }

    @Override
    public void close() {
        try {
            closer.close();
        } catch (IOException ignored) {
        }
    }

    private static String labelFor(Socket s) {
        InetAddress local = s.getLocalAddress();
        if (s.getInetAddress() != null && s.getInetAddress().isLoopbackAddress()) return "USB (adb)";
        try {
            NetworkInterface ni = NetworkInterface.getByInetAddress(local);
            return ni == null ? "Rede" : labelForInterface(ni.getName());
        } catch (SocketException e) {
            return "Rede";
        }
    }

    private static String labelForInterface(String name) {
        if (name.startsWith("usb") || name.startsWith("rndis") || name.startsWith("ncm")) return "USB";
        if (name.startsWith("wlan") || name.startsWith("swlan") || name.startsWith("ap") || name.startsWith("wifi")) return "Wi-Fi";
        return "Rede";
    }

    // ---------- busca na rede local ----------

    static final class Found {
        String pid;
        String pc;
        String target;
        String label;
    }

    /** Pergunta "tem Atalhos PC ai?" em todas as redes do aparelho: Wi-Fi e ancoragem USB. Bloqueia por windowMs. */
    static List<Found> discover(int windowMs) {
        List<Found> found = new ArrayList<>();
        HashSet<String> seen = new HashSet<>();
        try (DatagramSocket s = new DatagramSocket()) {
            s.setBroadcast(true);
            byte[] ask = ASK.getBytes(StandardCharsets.US_ASCII);
            for (InetAddress broadcast : broadcasts()) {
                try {
                    s.send(new DatagramPacket(ask, ask.length, broadcast, DEFAULT_PORT));
                } catch (IOException ignored) {
                    // uma interface sem rota nao pode impedir as outras
                }
            }
            long end = System.currentTimeMillis() + windowMs;
            byte[] buf = new byte[1024];
            while (true) {
                int left = (int) (end - System.currentTimeMillis());
                if (left <= 0) break;
                s.setSoTimeout(left);
                DatagramPacket p = new DatagramPacket(buf, buf.length);
                try {
                    s.receive(p);
                } catch (SocketTimeoutException e) {
                    break;
                }
                try {
                    JSONObject o = new JSONObject(new String(p.getData(), 0, p.getLength(), StandardCharsets.UTF_8));
                    if (!"AtalhosPC".equals(o.optString("app"))) continue;
                    Found f = new Found();
                    f.pid = o.getString("pid");
                    f.pc = o.optString("pc", "PC");
                    f.target = "tcp:" + p.getAddress().getHostAddress() + ":" + o.optInt("port", DEFAULT_PORT);
                    f.label = labelForAddress(p.getAddress());
                    if (seen.add(f.target)) found.add(f);
                } catch (JSONException ignored) {
                }
            }
        } catch (IOException ignored) {
        }
        return found;
    }

    private static List<InetAddress> broadcasts() throws IOException {
        List<InetAddress> list = new ArrayList<>();
        for (NetworkInterface ni : Collections.list(NetworkInterface.getNetworkInterfaces())) {
            if (!ni.isUp() || ni.isLoopback()) continue;
            for (InterfaceAddress a : ni.getInterfaceAddresses()) {
                if (a.getBroadcast() != null) list.add(a.getBroadcast());
            }
        }
        list.add(InetAddress.getByName("255.255.255.255"));
        return list;
    }

    private static String labelForAddress(InetAddress peer) {
        if (!(peer instanceof Inet4Address)) return "Rede";
        byte[] p = peer.getAddress();
        try {
            for (NetworkInterface ni : Collections.list(NetworkInterface.getNetworkInterfaces())) {
                for (InterfaceAddress a : ni.getInterfaceAddresses()) {
                    if (!(a.getAddress() instanceof Inet4Address)) continue;
                    if (sameSubnet(p, a.getAddress().getAddress(), a.getNetworkPrefixLength())) return labelForInterface(ni.getName());
                }
            }
        } catch (SocketException ignored) {
        }
        return "Rede";
    }

    private static boolean sameSubnet(byte[] a, byte[] b, int prefix) {
        for (int bit = 0; bit < prefix; bit++) {
            int mask = 0x80 >> (bit % 8);
            if ((a[bit / 8] & mask) != (b[bit / 8] & mask)) return false;
        }
        return true;
    }
}
