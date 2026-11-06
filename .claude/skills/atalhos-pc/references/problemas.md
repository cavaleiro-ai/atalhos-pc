# Problemas por sintoma

Antes de tudo, rode `scripts\diagnostico.ps1`. Para ver o que aconteceu, leia `%APPDATA%\AtalhosPC\atalhos.log`.

## Instalação

| Sintoma | Causa provável | Ação |
|---|---|---|
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` | O APK instalado tem outra assinatura, por exemplo depois de recompilar com chave nova | Desinstalar apaga o pareamento e o cache: confirme com o usuário, desinstale e pareie de novo. |
| `INSTALL_FAILED_USER_RESTRICTED` ou recusa sem motivo | Política do aparelho ou Play Protect | O usuário aprova na tela, ou instale manualmente. Em Xiaomi, veja `aparelhos.md`. |
| `INSTALL_FAILED_OLDER_SDK` | Android abaixo de 5.0 | Não há como instalar nesse aparelho. |
| Windows bloqueia o `.exe` ou o instalador | SmartScreen, pois não há assinatura digital | **Mais informações** > **Executar assim mesmo**. |
| `instalar.ps1` diz que o instalador falhou | O motivo está no registro | Leia `%TEMP%\AtalhosPC-instalador.log`. Os casos comuns estão nas linhas abaixo. |
| Instalador: não conseguiu fechar o Atalhos PC | Programa de outra versão travado, ou aberto por outro usuário | Peça ao usuário para sair pela bandeja (botão direito, **Sair**) e rode de novo. |
| Instalador: falta espaço | A biblioteca precisa de cerca de 1,6 GB livres, mais 50 MB de folga | Instale sem a biblioteca, ou peça ao usuário para liberar espaço. |
| Instalador: sem permissão na pasta | Pasta protegida, como `C:\Program Files` | Use a pasta padrão, `%LOCALAPPDATA%\Programs\AtalhosPC`. Não rode como administrador só para isso. |
| Instalador ou `Start-Process -Wait` nunca termina no script | Com `/abrir`, o PowerShell 5.1 espera também o programa aberto | Use `-PassThru` e `.WaitForExit()`, como o `instalar.ps1`. |
| Depois de instalar, o firewall pergunta de novo | A regra é ligada ao caminho do `.exe`, e a pasta instalada é nova | O usuário clica em **Permitir acesso**, ou usa o botão do programa. |
| Depois de desinstalar, a pasta do programa continua | Há arquivos que o instalador não colocou lá | É de propósito: o desinstalador só apaga o que está em `arquivos-instalados.txt`. Mostre ao usuário o que sobrou. |
| A saudação não aparece mais ao abrir | Ela aparece uma vez por PC (`greeted` no `config.json`) | Normal. O quadro preto da janela a mostra de novo. |
| Programa não abre, erro de .NET | .NET Framework ausente | Instalar o .NET Framework 4.5+ pelo site da Microsoft. |
| `instalar.ps1` diz arquivo alterado | Hash não confere | Não continue. Copie a skill de novo de uma fonte confiável. |
| `build.ps1` não substitui o `.exe` | Programa aberto | Feche o programa pela bandeja antes. |

## Conexão

| Sintoma | Causa provável | Ação |
|---|---|---|
| "Nada encontrado" na lista de PCs | Programa fechado, firewall ou redes diferentes | Veja a janela do programa e o diagnóstico. Teste por adb para separar rede de programa. |
| Conecta por adb e não pela rede | Firewall | Passo 4 da skill. |
| Conecta por USB e não por Wi-Fi | Sub-redes diferentes, rede de convidados ou isolamento de clientes | Compare os IPs. Na mesma sub-rede, tente digitar o IP. Não mexa no roteador sem o usuário pedir. |
| Rede Pública no Windows | A regra do aviso pode cobrir só Particular | Botão do firewall no programa. |
| Moveu o `.exe` e parou | A regra do firewall é ligada ao caminho | Libere de novo. |
| Ancoragem ligada e o PC sem rede nova | Falta o driver RNDIS ou cabo só de carga | Teste outro cabo e veja a Ancoragem no Gerenciador de Dispositivos. |
| O PC perdeu a internet ou mudou de rota | A ancoragem virou a rota padrão | Avise o usuário. Se atrapalhar, use USB por adb. |
| `USB por adb: nenhum aparelho autorizado` | Depuração não aprovada | O usuário aprova na tela do aparelho. |
| `USB por adb: adb.exe não encontrado` | adb fora dos lugares procurados | Coloque ao lado do `.exe` ou no PATH. |
| `Bluetooth: Sem adaptador disponível` | PC sem adaptador, ou desligado | Sem solução por software. Ofereça USB ou um adaptador USB; ele é detectado em até 30 s. |
| Bluetooth não conecta com adaptador presente | Este caminho nunca foi testado de ponta a ponta | Confirme o pareamento no sistema e a permissão no app. Se persistir, diga ao usuário e use USB. |
| "Parear de novo" | Aparelho removido no PC, ou dados do PC apagados | Gere novo código. |
| Código errado várias vezes | 5 erros fecham o pareamento | Gere outro. Letras e números não têm 0, O, 1, I nem L. |
| Toque espera e dá erro em 8 s | App sem conexão | Veja a luz do visor. |

## Atalhos

| Sintoma | Causa provável | Ação |
|---|---|---|
| Tecla apagada | Arquivo mudou de lugar | Remova e adicione de novo. |
| Abriu, mas atrás das outras janelas | Windows protege o foco | Normal: clique na barra de tarefas. |
| Pede confirmação de administrador | UAC do programa aberto | Alguém precisa aceitar na tela do PC. |
| Ícone genérico | O Windows não tem ícone melhor para o arquivo | Normal: é o mesmo do Explorer. |
| A janela do PC mostra só os nomes dos atalhos, sem as imagens, depois de reabrir o programa | Erro do programa até a versão 2.1. O tablet mostra os ícones normalmente | Atualize o programa com `instalar.ps1 -SoPc -Substituir`, depois de confirmar com o usuário, porque isso fecha o programa. Nada se perde. |
| Programa iniciado com o Windows não mostra a janela ao ser aberto de novo | O mesmo erro | A mesma atualização. Antes dela, use um clique duplo no ícone da bandeja. |
| Atalho da tela inicial não aparece | Launcher sem suporte, ou Android abaixo de 8 | A opção some do menu nesses casos. |

## Sons

| Sintoma | Causa provável | Ação |
|---|---|---|
| Pad mostra erro ao tocar | Formato sem codec no Windows | Converta para MP3, o único formato testado: `ffmpeg -i ENTRADA -b:a 160k SAIDA.mp3`. |
| Medidor mexe e não se ouve | Saída padrão errada ou volume do Windows baixo | Confira no Windows. O app usa só a saída padrão. |
| `SILENCIO` e nada no medidor | Mudo aceso, volume zerado ou arquivo mudo | Veja se o botão diz **Sem som** e a posição dos faders. |
| Som muito baixo em 50% | A curva do volume é ao quadrado, como o ouvido percebe | Normal. Medido: de 80% para 40%, o pico cai a um quarto. |
| Medidor mexe sem tocar nada | Mede toda a saída do PC | Normal: inclui música e vídeo de outros programas. |
| Sons de exemplo não aparecem | Pasta `sons` não estava ao lado do `.exe` na primeira abertura | O programa só tenta uma vez. Arraste os arquivos para a aba Sons. |
| Removi um exemplo e ele não voltou | É intencional | Adicione o arquivo de novo. |
| Biblioteca diz que não está instalada | A pasta `biblioteca` não está ao lado do `.exe`, ou não tem subpastas com áudio | `instalar.ps1 -SoBiblioteca -Biblioteca PASTA`. A skill não traz os áudios. |
| No app, a biblioteca pede para atualizar o programa do PC | Programa anterior à versão 2.2 responde `Comando desconhecido` | `instalar.ps1 -SoPc -Substituir`, depois de confirmar com o usuário. |
| `O Windows não conseguiu abrir este som: Problema ao inicializar o MCI` | Erro 277. A partir da 2.2, o tocador já tenta sem a etiqueta ID3v2. Se continuar, o arquivo tem pouco ou nenhum áudio | Teste com `ffprobe`. Se tiver áudio, converta: `ffmpeg -i ENTRADA -b:a 160k SAIDA.mp3`. |
| Categorias abrem ou sons entram sem você tocar, durante um teste | Alguém está usando o aparelho, e os toques se misturam | Confira com `getevent` (veja as regras de conduta) e pare até o aparelho ficar livre. Não apague o que a pessoa adicionou. |
| Pad sem desenho, com ♪ | Som sem ícone próprio | Segure o pad > **Trocar ícone**. |

## Aparência

| Sintoma | Causa provável | Ação |
|---|---|---|
| Teclas somem ao tocar em **Atalhos** ou ao mudar algo em **Ajustes**, e voltam ao reabrir o app | Erro da versão 2.0, no tamanho Grande | Atualize para a 2.1 ou mais nova: `instalar.ps1 -SoAparelho`. Os atalhos não se perdem, pois ficam no PC. Confirme com `conferir_teclas.ps1`. |
| Tecla não acende nem afunda | Versão antiga em aparelho com animações em 0x | Atualize. A versão atual usa relógio próprio. |
| Barras do Android somem | O app ocupa a tela inteira | Deslize da borda. |
| Nome cortado | Botão pequeno | Use um tamanho maior ou esconda os nomes. |

## Dicas para quem dirige pelo adb

- No PowerShell 5.1, redirecionar `adb exec-out` corrompe PNG. Use `tocar_ui.ps1 -Print`.
- Em `input text`, troque espaços por `%s`.
- `uiautomator dump` falha com a tela apagada ou bloqueada. Ligue e desbloqueie antes; se houver senha, peça ao usuário.
- Para simular celular, use `wm size 720x1600` e `wm density 320`, e desfaça com `wm size reset` e `wm density reset`. Isso não substitui um teste em celular real.
