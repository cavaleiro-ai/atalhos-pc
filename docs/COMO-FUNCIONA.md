# Como funciona o Atalhos PC

## Visão geral

```
Android (app em Java, sem bibliotecas)             Windows (AtalhosPC.exe, C#/.NET Framework)
                                                   Instalar-AtalhosPC.exe  instala, atualiza e remove
  MainActivity + DeckView + KeyView  teclas e pads   MainForm     janela e bandeja
  MixerViews    faders e medidor                   Sound        MCI e medidor Core Audio
  Theme / SettingsActivity / IconPicker
  ConnectActivity / BrowseActivity                   Server       TCP, UDP e RFCOMM
  RunActivity   atalho da tela inicial               Store        config.json, DPAPI
  Hub           conexão automática e cache   <--->   Shell        ícone real e ShellExecute
  Link/Channel/Crypto  protocolo seguro              Proto        protocolo seguro
  Transport     TCP (Wi-Fi/USB) e Bluetooth          Bluetooth    Winsock AF_BTH
```

Os três caminhos entregam um fluxo de bytes, e por cima dele roda sempre o mesmo protocolo autenticado:

| Caminho | Como chega ao PC |
|---|---|
| Wi-Fi | TCP na porta 47810, achado por broadcast UDP na mesma porta. |
| USB por Ancoragem | A ancoragem cria uma rede entre os dois (interface `usb0`/RNDIS). Daí em diante é igual ao Wi-Fi. |
| USB por adb | `adb reverse tcp:47810 tcp:47810` faz o `127.0.0.1:47810` do aparelho chegar ao PC, sem passar pelo firewall. |
| Bluetooth | RFCOMM, serviço publicado com o UUID `7a3f1c52-8e44-4d6b-9b21-5f0c7e1a2d90`. |

## O ícone

`Shell.cs` usa `IShellItemImageFactory`, a mesma origem do Explorer, e pede a imagem em 256 px. Depois:

1. Converte o HBITMAP de alfa pré-multiplicado para alfa direto.
2. Corta a sobra transparente e amplia. Ícones antigos de 32 px vêm pequenos no meio da imagem, e a tecla ficaria vazia.
3. Guarda o PNG em `%APPDATA%\AtalhosPC\icones\`, com uma chave derivada do caminho e da data do arquivo.

O app guarda o PNG pela mesma chave e só busca de novo quando ela muda. Na tecla, a tela é preenchida com a cor média do ícone, escurecida. Um ícone quadrado e opaco ocupa a tela inteira, como numa tecla LCD.

## Sons

`Sound.cs` toca com MCI (`winmm`, tipo `mpegvideo`), que já vem no Windows e sobrepõe vozes, até 32. Toda chamada passa por uma thread só, porque o MCI liga o dispositivo à thread que o abriu. A cada 80 ms a mesma thread fecha as vozes que terminaram e avisa os aparelhos com `{t:"sound_state", playing}`.

O volume efetivo é `som × geral / 100`, ou zero com Mudo, e vai ao MCI com curva ao quadrado (0 a 1000), mais próxima do ouvido do que a reta. Por isso 50% soa bem mais baixo que 100%: medido, 80% para 40% divide o pico por 4.

Durante o arraste, o volume muda só na memória; ao soltar (`final`), é gravado e os outros aparelhos são avisados. Assim o disco não é gravado a cada movimento.

O medidor lê `IAudioMeterInformation` da saída padrão, a cada 70 ms, só enquanto algum aparelho estiver na área de Sons. Em silêncio quase nada é enviado.

Os sons de exemplo são copiados uma única vez de `sons\` (ao lado do `.exe`) para `%APPDATA%\AtalhosPC\sons`, com o PNG de mesmo nome como ícone. Se o usuário remover um, ele não volta. `assets\baixar_sons_exemplo.py` refaz a pasta, conferindo as licenças pela API do Wikimedia Commons.

O ícone de um som fica em `icones-sons\<id>.png`. A imagem é decodificada e regravada em 256 px, com limite de 8 MB. Emojis são desenhados no próprio aparelho e enviados como PNG.

**MP3 recusado pelo MCI:** certas etiquetas ID3v2 fazem o MCI recusar um MP3 válido, com o erro 277. Quando a abertura falha, `MciPlayer` grava uma cópia sem a etiqueta em `%APPDATA%\AtalhosPC\som-sem-etiqueta\` e abre a cópia. O arquivo original não muda.

### Biblioteca

`Library.cs` lê `biblioteca\`, ao lado do `.exe`: cada subpasta com áudio é uma categoria, e `<pasta>.png` é o ícone dela. Os títulos das pastas originais, em inglês, são traduzidos no código; uma pasta nova usa o próprio nome. O índice fica na memória e é refeito a cada abertura da biblioteca.

- **Busca:** compara sem acento e sem maiúscula, e exige todas as palavras, em qualquer ordem.
- **Páginas:** o aparelho recebe no máximo 200 itens por pedido e pede mais ao rolar. A janela do PC usa uma `ListView` virtual, que só monta as linhas visíveis.
- **Adicionar:** o som continua apontando para o arquivo na biblioteca, sem cópia. O ícone da categoria é copiado para `icones-sons\<id>.png`, e assim o usuário pode trocá-lo, e remover o som apaga só essa cópia.
- **Prévia:** toca com o id `biblioteca-previa`, uma por vez, com volume de 80% vezes o volume geral.
- **Segurança:** o aparelho nunca envia caminho. `lib_preview` e `lib_add` recebem categoria e nome de arquivo, procurados no índice; o que não estiver lá é recusado, e assim `..\` ou um caminho absoluto não alcançam nada. Por isso a biblioteca não depende de **Permitir navegar**.

## Temas e tamanhos

`Theme.java` define tudo de cada tema: mesa, chassi, luz da borda (`SweepGradient`), capas, telas, textos e a paleta de pads. O halo da borda são cinco traços cada vez mais largos e transparentes, porque `BlurMaskFilter` não funciona com aceleração de hardware em versões antigas do Android. As telas leem o tema ao serem montadas; trocar de tema monta a tela de novo.

`DeckView` escolhe as colunas pela tabela de tamanho (5/6/8/10 em teclas e 4/5/7/9 em pads, deitado; 3/4/5/6 em pé) e calcula as linhas pela área livre, mantendo os botões quadrados. As teclas são criadas sob demanda.

## Protocolo

Frames: `tamanho (4 bytes big endian) | conteúdo`. O conteúdo é JSON. Antes da autenticação o limite é de 4 KB, depois 32 MB.

### Pareamento

```
aparelho -> PC   {t:"pair", v:1, dev, name, cn}
aparelho <- PC   {t:"pair_ch", sn, pid, pc, port}      ou {t:"pair_closed"}
                 chave = PBKDF2-HMAC-SHA256(código, cn|sn, 100000)
aparelho -> PC   {t:"pair_proof", proof = HMAC(chave, "client"|cn|sn)}
aparelho <- PC   {t:"pair_ok", proof = HMAC(chave, "server"|cn|sn), box}      ou {t:"pair_bad", left}
```

`box` é o segredo de 32 bytes do aparelho, cifrado com chaves derivadas do código. O código nunca trafega, e cada lado prova que o conhece.

### Sessão

```
aparelho -> PC   {t:"hello", v:1, dev, cn}
aparelho <- PC   {t:"hello", v:1, pid, pc, port, known, sn}      (fecha se known=false)
                 chaves por direção = HMAC(segredo, "enc-c2s"|cn|sn) e equivalentes
aparelho -> PC   frame cifrado {t:"auth"}
aparelho <- PC   frame cifrado {t:"ok"}
```

Frame cifrado: `iv (16) | AES-256-CBC | HMAC-SHA256(seq | iv | cifrado)`. A assinatura é conferida antes de decifrar. O contador `seq` e os nonces de cada sessão impedem repetir frames gravados.

### Comandos

Pedidos levam `r` (número do pedido), e a resposta traz o mesmo `r` com `ok` e `err`. O PC também envia por conta própria `{t:"changed"}` quando a lista muda, `{t:"sound_state"}` quando um som começa ou termina, e `{t:"meter"}` com o nível.

| Comando | Campos | Observação |
|---|---|---|
| `ping` | | A cada 15 s; sem resposta em 45 s a conexão cai. |
| `list` | | Atalhos e sons com id, nome, caminho e chave do ícone, mais volume geral, mudo e sons tocando. |
| `run` | `id` | Abre um atalho da lista. |
| `icon` | `id` ou `path` | PNG em base64. Por `path` exige navegação ligada. |
| `rename`, `move`, `remove` | `id` e dados | |
| `places`, `browse`, `add`, `open`, `sound_add` | `path` | Exigem **Permitir navegar** ligado no PC. |
| `sound_play`, `sound_stop` | `id` (sem `id`, para tudo) | Só toca sons da lista. |
| `sound_volume`, `sound_master` | `volume`, `muted`, `final` | |
| `sound_seticon` | `id` e `png` ou `path` | Por `path` exige navegação ligada. |
| `meter` | `on` | Liga os avisos `{t:"meter", p}` nesta conexão. |
| `lib_categories` | | Categorias com nome, quantidade e se têm ícone, mais o total. Refaz o índice. |
| `lib_list` | `cat`, `q`, `from`, `limit` | Sons de uma categoria, ou de todas com `cat` vazio. Até 200 por pedido. |
| `lib_icon` | `cat` | PNG da categoria em base64. |
| `lib_preview`, `lib_add` | `cat`, `file` | Só aceitam o que está no índice. |
| `lib_stop` | | Para a prévia. |

## Segurança

**Protege contra:**

- Alguém na mesma rede abrir arquivos no PC: sem o segredo, nenhum comando é aceito.
- Adivinhar o código: o pareamento só abre por clique no PC, dura 3 minutos, vale para um aparelho e fecha após 5 erros.
- Ler ou alterar o tráfego, ou repetir frames gravados.
- Outro app do aparelho disparar abertura: `RunActivity` não é exportada.
- Roubo do segredo no PC por outro usuário do Windows: ele fica cifrado com DPAPI.

**Não protege contra:**

- Quem tem um aparelho pareado: ele abre qualquer arquivo com as permissões do seu usuário. Desligar **Permitir navegar** restringe à lista de atalhos.
- Um aparelho com root: o segredo fica nas preferências privadas do app, sem Android Keystore.
- Quem gravar a rede exatamente durante o pareamento e tentar o código por força bruta depois. O código tem cerca de 30 bits e o PBKDF2 encarece cada tentativa, mas não é uma troca de chaves com sigilo futuro. Em rede de terceiros, pareie por USB.
- Malware já rodando no seu usuário do Windows.

**Firewall:** o botão do programa cria uma regra só para o `.exe` e só para a sub-rede local (`remoteip=localsubnet`), em qualquer perfil, porque a rede da Ancoragem USB costuma vir como Pública. A regra criada pelo aviso do próprio Windows não tem essa restrição de IP. Em ambos, é a autenticação acima que protege.

## Instalador

`dist\Instalar-AtalhosPC.exe` é um programa C# próprio (`windows\setup\`), compilado pelo mesmo `csc.exe`, sem Inno Setup nem WiX. Dentro dele vão, como recursos: o `AtalhosPC.exe`, o `AtalhosPC.apk`, os sons de exemplo (`payload/...`), os 14 ícones das categorias da biblioteca e a logo. A biblioteca em si não vai.

| Arquivo | Papel |
|---|---|
| `setup\SetupProgram.cs` | Ponto de entrada: assistente, modo silencioso e desinstalação. |
| `setup\SetupForm.cs` | Assistente em quatro passos: Saudação, Opções, Instalação e Pronto. A cópia roda em outra thread. |
| `setup\Installer.cs` | Instala, atualiza e remove. Não tem interface, por isso o teste o chama direto. |
| `src\Greeting.cs` | Saudação do Cavaleiro AI, desenhada nas cores da marca. É usada pelo instalador e pelo programa. |
| `src\AppInfo.cs` | Versão, nome e os sinais nomeados que os dois exes compartilham. |

**O que a instalação faz, em ordem:**

1. Testa se consegue gravar na pasta. A padrão é `%LOCALAPPDATA%\Programs\AtalhosPC`, do próprio usuário, então nada pede administrador.
2. Fecha o programa aberto pelo sinal `Local\AtalhosPC.Sair` e espera até 10 s. Uma versão antiga, sem o sinal, é encerrada à força. Depois zera o sinal, para não fechar a cópia nova assim que ela abrir.
3. Grava cada arquivo como `.novo` e troca pelo antigo, para nunca deixar um exe pela metade.
4. Copia a si mesmo como `Desinstalar.exe`.
5. Biblioteca, se pedida: confere o espaço livre, copia só os áudios de primeiro nível de cada categoria (a mesma regra de `Library.cs`) e pula os que já estão lá com o mesmo tamanho.
6. Cria os atalhos `.lnk` pelo `WScript.Shell`. Um atalho desmarcado que aponte para esta cópia é removido.
7. Liga ou desliga **Iniciar junto com o Windows** (`Run\AtalhosPC`, o mesmo valor que o programa usa). Por padrão, a opção mantém o estado atual.
8. Grava `arquivos-instalados.txt` com tudo o que colocou. A lista é somada à da instalação anterior.
9. Cria a entrada de **Aplicativos** em `HKCU\...\Uninstall\AtalhosPC`, com versão, editor, tamanho, `UninstallString` e `QuietUninstallString`.

**A desinstalação** fecha o programa só se ele roda da pasta desinstalada e apaga só os caminhos da lista. Um caminho com `..` ou absoluto é ignorado. Ela também remove as pastas que ficaram vazias, os atalhos que apontam para esta cópia e o **Iniciar com o Windows** desta cópia. A entrada de **Aplicativos** só sai se for da mesma pasta. O próprio `Desinstalar.exe` não pode se apagar aberto: um `cmd` escondido espera 3 s, apaga o exe e roda `rmdir` sem `/s`, que só remove a pasta vazia. Nada em `%APPDATA%\AtalhosPC` é tocado.

**Linha de comando** (código de saída 0 deu certo, 1 erro, 2 cancelado; registro em `%TEMP%\AtalhosPC-instalador.log`):

```
Instalar-AtalhosPC.exe /silencioso [/pasta="C:\..."] [/biblioteca="C:\..."] [/sem-atalhos] [/iniciar-com-windows] [/abrir]
Desinstalar.exe /desinstalar [/silencioso] [/pasta="C:\..."]
```

No modo silencioso, a biblioteca só é copiada com `/biblioteca`. No assistente, a pasta `biblioteca` ao lado do instalador é sugerida sozinha.

**Saudação:** o instalador abre o programa com `--saudacao-vista`, que marca `greeted` no `config.json`. Quem abre o programa sem instalar vê a saudação uma vez, no primeiro `Shown` da janela, e a marca fica gravada.

**Sem assinatura digital:** sem certificado de assinatura de código, o SmartScreen avisa em arquivos baixados. O manifesto padrão do `csc` declara `asInvoker`, então o Windows não trata o exe como instalador que exige administrador.

## Onde ficam os dados

| Dado | Local |
|---|---|
| Programa instalado | `%LOCALAPPDATA%\Programs\AtalhosPC\`, com `arquivos-instalados.txt` e `Desinstalar.exe` |
| Registro do instalador | `%TEMP%\AtalhosPC-instalador.log` |
| Atalhos, aparelhos, opções | `%APPDATA%\AtalhosPC\config.json` |
| Ícones | `%APPDATA%\AtalhosPC\icones\` e `icones-sons\` |
| Sons de exemplo copiados | `%APPDATA%\AtalhosPC\sons\` |
| Biblioteca de sons | `biblioteca\`, ao lado do `.exe` |
| Cópias de MP3 sem etiqueta | `%APPDATA%\AtalhosPC\som-sem-etiqueta\` |
| Tema, tamanhos e nomes | Preferências do app, em cada aparelho |
| Registro de conexões e aberturas | `%APPDATA%\AtalhosPC\atalhos.log` (girado em 512 KB) |
| Início com o Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| Dados do app | Armazenamento privado do app, fora do backup do Android |

## Compilar

Tudo sem Visual Studio nem Android Studio.

**Windows:** `windows\build.ps1` usa o `csc.exe` que vem com o .NET Framework. Feche o programa antes, porque o Windows não deixa substituir um `.exe` em uso. O `dist\AtalhosPC.apk` precisa existir, porque vai dentro do instalador. O script gera o programa, o instalador e os dois testes:

```powershell
windows\build.ps1
windows\build\ProtocolTest.exe     # protocolo, sons, biblioteca e janela: 90 testes
windows\build\InstaladorTest.exe   # instalador em pastas e chaves de teste: 56 testes
```

O `InstaladorTest` não cria atalho na sua área de trabalho, não mexe na entrada real de **Aplicativos** e não fecha o programa aberto. Ele abre o assistente na tela por alguns segundos. Com uma pasta como segundo argumento, salva as capturas de cada tela.

**Android:** `android\build.ps1` roda javac, d8, aapt2, zipalign e apksigner. Se faltarem ferramentas, `android\setup-tools.ps1` baixa cerca de 310 MB das fontes oficiais (Temurin 17, build-tools 34 e plataforma 34) e confere os hashes.

```powershell
android\build.ps1
```

Na primeira compilação é criada a chave `android\atalhospc.keystore`, com a senha em `atalhospc.keystore.senha`. Guarde as duas: atualizar o app exige a mesma assinatura. **Não publique esses arquivos.** O `.gitignore` já os exclui.

**Logo:** `assets\make_icons.py` (requer Pillow) desenha a logo e gera `app.ico` e os PNGs do Android.

**Skill:** depois de compilar, `assets\empacotar_skill.ps1` atualiza programa, app, sons, documentação, código-fonte e hashes dentro da skill, sem a chave de assinatura.

## Decisões que evitam dor de cabeça

- **Tecla nova é sempre preenchida.** `DeckView` cria as teclas em `onMeasure`, depois de `setItems`, e cada troca de aba ou de ajuste monta um `DeckView` novo. Na versão 2.0, o preenchimento só acontecia se a grade mudasse de tamanho, e o Grande de Atalhos tem exatamente as 15 teclas do valor inicial: a tela ficava vazia.
- **A janela do PC cria o handle no construtor.** `OnUi` descarta o que chega sem handle, e os ícones do cache chegam antes de a janela aparecer. Até a versão 2.1, a lista reaberta ficava só com os nomes, e o programa iniciado na bandeja não atendia a um segundo clique. Não tire o `CreateHandle()` de `MainForm`.
- **A biblioteca fica fora da skill e do código-fonte.** São 1,6 GB de áudios de terceiros, sem licença conferida. Só os ícones das categorias, do Noto Emoji (Apache 2.0), são distribuídos. Em outro PC, `instalar.ps1 -Biblioteca PASTA` copia uma coleção que o usuário já tenha.
- **Para testar o instalador por script, espere só o processo do instalador.** No PowerShell 5.1, `Start-Process -Wait` espera também os filhos, e com `/abrir` o programa reaberto continua rodando: o script fica parado para sempre. Use `-PassThru` e `.WaitForExit()`.
- **O instalador desinstala pela lista, nunca pela pasta inteira.** Assim, uma pasta mal escolhida, como `Documentos`, não perde nada do usuário.
- **Sem `ValueAnimator` nas teclas.** Aparelhos com a escala de animação em 0x, comum em quem otimiza o tablet, pulam direto para o fim e o efeito some. `KeyView` anima pelo próprio relógio.
- **Interface em código, sem XML nem bibliotecas.** O APK fica com pouco mais de 100 KB e o build não depende de Gradle.
- **`build.ps1` do Android usa `$ErrorActionPreference = "Continue"`.** No PowerShell 5.1, avisos do javac no stderr viram erro fatal com `Stop`. Cada passo confere `$LASTEXITCODE`.
- **Alterou o protocolo? Mude os dois lados.** `Proto.cs` e `Crypto.java`/`Channel.java` são espelhos, assim como o UUID em `Bluetooth.cs` e `Transport.java`.
