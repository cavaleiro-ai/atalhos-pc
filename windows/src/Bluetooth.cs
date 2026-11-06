// Servidor RFCOMM usando o Winsock do proprio Windows: nao precisa de biblioteca externa.
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AtalhosPC
{
    public static class Bth
    {
        public const int AF_BTH = 32;
        public const int BTHPROTO_RFCOMM = 3;
        public const int SockAddrSize = 30; // SOCKADDR_BTH e empacotado (pack 1)
        const int NS_BTH = 16;
        const int RNRSERVICE_REGISTER = 0;
        const int SOCK_STREAM = 1;

        // Mesmo UUID em android/src/.../Transport.java
        public static readonly Guid ServiceId = new Guid("7a3f1c52-8e44-4d6b-9b21-5f0c7e1a2d90");

        [StructLayout(LayoutKind.Sequential)]
        public struct SOCKET_ADDRESS
        {
            public IntPtr lpSockaddr;
            public int iSockaddrLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CSADDR_INFO
        {
            public SOCKET_ADDRESS LocalAddr;
            public SOCKET_ADDRESS RemoteAddr;
            public int iSocketType;
            public int iProtocol;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WSAQUERYSET
        {
            public int dwSize;
            public IntPtr lpszServiceInstanceName;
            public IntPtr lpServiceClassId;
            public IntPtr lpVersion;
            public IntPtr lpszComment;
            public int dwNameSpace;
            public IntPtr lpNSProviderId;
            public IntPtr lpszContext;
            public int dwNumberOfProtocols;
            public IntPtr lpafpProtocols;
            public IntPtr lpszQueryString;
            public int dwNumberOfCsAddrs;
            public IntPtr lpcsaBuffer;
            public int dwOutputFlags;
            public IntPtr lpBlob;
        }

        [DllImport("ws2_32.dll", EntryPoint = "WSASetServiceW", SetLastError = true)]
        static extern int WSASetService(ref WSAQUERYSET query, int operation, int flags);

        public sealed class EndPointBth : EndPoint
        {
            public readonly int Channel;
            public EndPointBth(int channel) { Channel = channel; }

            public override AddressFamily AddressFamily { get { return (AddressFamily)AF_BTH; } }

            public override SocketAddress Serialize()
            {
                // familia(2) | endereco(8) | servico(16) | canal(4)
                SocketAddress a = new SocketAddress((AddressFamily)AF_BTH, SockAddrSize);
                uint ch = unchecked((uint)Channel);
                for (int i = 0; i < 4; i++) a[26 + i] = (byte)(ch >> (8 * i));
                return a;
            }

            public override EndPoint Create(SocketAddress a)
            {
                uint ch = 0;
                for (int i = 0; i < 4; i++) ch |= (uint)a[26 + i] << (8 * i);
                return new EndPointBth(unchecked((int)ch));
            }
        }

        // Publica o servico para o Android achar pelo UUID. A memoria fica alocada enquanto o processo existir,
        // que e o tempo em que o registro precisa continuar valido.
        public static void Register(int channel, string name)
        {
            byte[] sockaddr = new byte[SockAddrSize];
            sockaddr[0] = AF_BTH;
            uint ch = unchecked((uint)channel);
            for (int i = 0; i < 4; i++) sockaddr[26 + i] = (byte)(ch >> (8 * i));
            IntPtr addr = Marshal.AllocHGlobal(SockAddrSize);
            Marshal.Copy(sockaddr, 0, addr, SockAddrSize);

            CSADDR_INFO cs = new CSADDR_INFO();
            cs.LocalAddr.lpSockaddr = addr;
            cs.LocalAddr.iSockaddrLength = SockAddrSize;
            cs.RemoteAddr.lpSockaddr = addr;
            cs.RemoteAddr.iSockaddrLength = SockAddrSize;
            cs.iSocketType = SOCK_STREAM;
            cs.iProtocol = BTHPROTO_RFCOMM;
            IntPtr csPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(CSADDR_INFO)));
            Marshal.StructureToPtr(cs, csPtr, false);

            byte[] guid = ServiceId.ToByteArray();
            IntPtr guidPtr = Marshal.AllocHGlobal(guid.Length);
            Marshal.Copy(guid, 0, guidPtr, guid.Length);

            WSAQUERYSET q = new WSAQUERYSET();
            q.dwSize = Marshal.SizeOf(typeof(WSAQUERYSET));
            q.lpszServiceInstanceName = Marshal.StringToHGlobalUni(name);
            q.lpszComment = Marshal.StringToHGlobalUni("Atalhos PC");
            q.lpServiceClassId = guidPtr;
            q.dwNameSpace = NS_BTH;
            q.dwNumberOfCsAddrs = 1;
            q.lpcsaBuffer = csPtr;

            if (WSASetService(ref q, RNRSERVICE_REGISTER, 0) != 0) throw new SocketException(Marshal.GetLastWin32Error());
        }
    }
}
