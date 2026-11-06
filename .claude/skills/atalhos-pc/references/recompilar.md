# Recompilar e alterar

Recompile só se o usuário pedir uma mudança, ou se os binários de `assets/` não servirem. Tudo funciona sem Visual Studio e sem Android Studio.

## Preparar

Extraia `assets/codigo-fonte.zip` em uma pasta nova de trabalho, de preferência dentro do projeto do usuário. O pacote não traz a chave de assinatura nem a pasta `dist`. Para o instalador, crie `dist\` e copie para ela a pasta `assets\sons` da skill; o `AtalhosPC.apk` vem do `android\build.ps1` (ou, se o app não mudou, copie o de `assets\`).

## Compilar

```powershell
android\build.ps1                  # javac, d8, aapt2, zipalign e apksigner, sem Gradle; rode antes, o APK vai dentro do instalador
windows\build.ps1                  # feche o AtalhosPC.exe antes; gera o programa, o instalador e os dois testes
windows\build\ProtocolTest.exe     # deve terminar com "90 ok, 0 falha(s)"; monta a janela sem mostrá-la, não abre nada nem toca som
windows\build\InstaladorTest.exe   # deve terminar com "56 ok, 0 falha(s)"; instala em pastas e chaves de teste e abre o assistente na tela
```

Se faltarem ferramentas, `android\build.ps1` chama `android\setup-tools.ps1`, que baixa cerca de 310 MB das fontes oficiais (Temurin 17, build-tools 34, plataforma 34) e confere os hashes. `-Tools PASTA` usa ferramentas já baixadas.

Os binários saem em `dist\`: `AtalhosPC.exe`, `Instalar-AtalhosPC.exe` e `AtalhosPC.apk`. O instalador leva dentro de si o `AtalhosPC.exe`, o `AtalhosPC.apk` e os sons de `dist\sons` do momento da compilação. Mudou o programa ou o app? Rode `windows\build.ps1` de novo, senão o instalador entrega a versão antiga. A versão fica em `windows\src\AppInfo.cs`.

## Assinatura

A primeira compilação cria `android\atalhospc.keystore` e `atalhospc.keystore.senha`. Essa chave é diferente da usada no APK que veio na skill, então **um aparelho com o APK da skill não aceita atualização por cima**: será preciso desinstalar, o que apaga o pareamento e o cache de ícones. Avise o usuário antes. Os atalhos e sons ficam no PC e não se perdem.

Guarde as duas peças, porque todas as atualizações futuras precisam delas, e nunca as publique.

## Mapa do código

| Quero mudar | Arquivo |
|---|---|
| Aparência da tecla ou do pad | `android/.../KeyView.java` |
| Grade, páginas, tamanhos | `android/.../DeckView.java` |
| Tela principal e mesa de som | `android/.../MainActivity.java` |
| Faders, medidor | `android/.../MixerViews.java` |
| Temas | `android/.../Theme.java` |
| Chassi, botões, cores | `android/.../Ui.java` |
| Lista de emojis | `android/.../IconPicker.java` |
| Conexão automática, cache | `android/.../Hub.java` |
| Janela do PC | `windows/src/MainForm.cs` |
| Comandos no PC | `windows/src/Server.cs` |
| Tocar som e medir | `windows/src/Sound.cs` |
| Biblioteca: índice e busca | `windows/src/Library.cs` |
| Biblioteca: janela do PC | `windows/src/LibraryForm.cs` |
| Biblioteca: tela do app | `android/.../LibraryActivity.java` |
| Comandos da biblioteca | `windows/src/Server.cs`, comandos `lib_*` |
| Ícones do Windows | `windows/src/Shell.cs` |
| Configuração | `windows/src/Store.cs` |
| Versão, sinais entre os exes | `windows/src/AppInfo.cs` |
| Saudação do Cavaleiro AI | `windows/src/Greeting.cs` |
| Instalador: assistente | `windows/setup/SetupForm.cs` |
| Instalador: instalar e desinstalar | `windows/setup/Installer.cs` |
| Instalador: linha de comando | `windows/setup/SetupProgram.cs` |
| Testes | `windows/tests/ProtocolTest.cs` e `windows/tests/InstallerTest.cs` |

## Cuidados que evitam retrabalho

- **Protocolo:** `Proto.cs` e `Crypto.java` + `Channel.java` são espelhos. O UUID está em `Bluetooth.cs` e `Transport.java`. Mude os dois lados e rode `ProtocolTest`.
- **Teclas:** `DeckView` cria as teclas em `onMeasure`, depois de `setItems`, e cada troca de aba ou de ajuste monta um `DeckView` novo. Tecla recém-criada precisa ser preenchida mesmo quando a grade tem o mesmo tamanho de antes.
- **Janela do PC:** `OnUi` descarta o que chega sem handle, e os ícones do cache chegam antes de a janela aparecer. Por isso `MainForm` chama `CreateHandle()` no construtor; não tire. Para ver um erro aqui, reabra o programa com os ícones já em cache, porque na primeira abertura eles demoram e chegam depois.
- **Biblioteca:** nunca aceite caminho vindo do aparelho, só categoria e nome procurados no índice, porque ela funciona mesmo com **Permitir navegar** desligado. Não coloque `dist/biblioteca` na skill nem no código-fonte: `assets\empacotar_skill.ps1` usa uma lista fixa justamente para isso.
- **Instalador:** a desinstalação apaga só o que está em `arquivos-instalados.txt`, nunca a pasta inteira, e ignora caminhos com `..` ou absolutos. Mantenha assim. Arquivo novo no programa entra pelo `build.ps1` como recurso `payload/...` e vai para a lista sozinho. O instalador fecha o programa pelo sinal `Local\AtalhosPC.Sair` (em `Program.cs`) e zera o sinal depois; se mudar esse nome, mude em `AppInfo.cs`, que vale para os dois.
- **Testar o instalador:** `InstaladorTest` usa pastas temporárias, chaves `HKCU\Software\AtalhosPC-Teste-*` e `CloseRunning = false`, então não fecha o programa do usuário. Ponta a ponta com o exe real abre o programa da pasta instalada, e o Windows pode perguntar sobre o firewall para esse caminho.
- **Animação:** não use `ValueAnimator` em nada essencial. Em aparelhos com a escala de animação em 0x, ele pula para o fim e o efeito some. `KeyView` usa relógio próprio.
- **Android antigo:** a API mínima é 21. Isole chamadas novas atrás de `Build.VERSION.SDK_INT`, como a classe `Pin`. Não use `java.util.function`, streams nem `List.sort`.
- **Windows:** o `csc.exe` é C# 5: sem `$""`, `?.`, `nameof` nem `out var`.
- **PowerShell 5.1:** `android\build.ps1` usa `$ErrorActionPreference = "Continue"` de propósito, pois avisos do javac no stderr viram erro fatal com `Stop`. Scripts `.ps1` devem ficar só em ASCII ou com BOM.
- **Configuração:** campo novo no `config.json` precisa de valor padrão, para quem atualiza não perder nada.
- **Sons de exemplo:** só com licença livre e crédito em `dist/sons/LICENCAS.md`, via `assets/baixar_sons_exemplo.py`.
- **Marcas:** a logo é própria. Não copie marcas de fabricantes de decks ou mesas de som.

## Depois de mudar

1. Rode `ProtocolTest` e `InstaladorTest` e teste no aparelho o que mudou, inclusive com `scripts\conferir_teclas.ps1` e no tamanho **Grande**, que é o padrão.
2. Rode `assets\empacotar_skill.ps1` para atualizar programa, instalador, app, sons, documentação, código e hashes dentro da skill.
3. Atualize `docs/` se o comportamento mudou.
