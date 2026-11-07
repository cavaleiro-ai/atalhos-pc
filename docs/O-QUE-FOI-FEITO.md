# O que foi feito no Atalhos PC

Registro completo do projeto: o que existe, como foi testado e o que ficou de fora. Datas de 31/10/2026.

## Versão 1: deck de atalhos

**Pedido:** um app para abrir qualquer arquivo do Windows pelo tablet, com o mesmo ícone, por Bluetooth, Wi-Fi ou USB.

- **Programa do Windows** (`AtalhosPC.exe`, C#, sem instalação): lista de atalhos com arrastar e soltar, bandeja do sistema, pareamento, início opcional com o Windows, liberação do firewall e túnel USB por adb.
- **App Android** (Java, sem bibliotecas, Android 5.0+): tela de teclas, conexão automática, navegação nas pastas do PC, atalho na tela inicial e funcionamento em tablet e celular.
- **Ícone real:** extraído pelo shell do Windows em 256 px, cortado e ampliado, sem a setinha de atalho.
- **Segurança:** pareamento por código de 6 caracteres que nunca trafega, segredo por aparelho guardado com DPAPI, canal AES-256-CBC com HMAC-SHA256 e proteção contra repetição.
- **Visual físico:** a partir da foto de um deck, chassi escuro com teclas em relevo que afundam e acendem com o resultado.

## Versão 2: sons, temas e tamanhos

**Pedido:** 10 temas, botões menores, opção de só logo e uma área de sons no estilo de uma mesa física, com volume por som.

- **Área de Sons:** pads iluminados por dentro, tocando no PC com até 32 vozes sobrepostas. Cada som tem volume e ícone próprios.
- **Mesa de som:** fader geral, fader do som selecionado, medidor do pico real da saída do Windows, botões Mudo e Parar.
- **11 sons de exemplo:** cachorro, gato, galo, pato, sapo, aplausos, viva, buzina, trovão, vidro e sino. Áudio do Wikimedia Commons e ícones Noto Emoji, com créditos em `dist/sons/LICENCAS.md`.
- **Ícone de som:** 64 emojis desenhados no aparelho ou imagem do PC.
- **10 temas:** Grafite, Preto Absoluto, RGB Gamer, Neon Cyber, Matrix, Tron, Aurora, Vulcão, Ouro Negro e Gelo, com luz de borda em degradê.
- **4 tamanhos de botão** e **opção de esconder nomes**, separados para Atalhos e Sons.
- **Aba Sons na janela do PC.**
- **Atualização sem perda:** atalhos e pareamento do usuário continuaram valendo.

## Versão 2.1: correção das teclas que sumiam

**Relato do usuário:** ao tocar na aba Atalhos, ou ao ir em Ajustes e voltar, os atalhos desapareciam. Em Ajustes, isso acontecia quando algo era alterado, porque a tela é remontada; sem alterar nada, as teclas permaneciam.

- **Causa:** cada troca de aba ou de ajuste monta uma grade nova, e os itens chegam antes de as teclas existirem. As teclas só eram preenchidas depois se a grade mudasse de tamanho. O tamanho Grande de Atalhos tem exatamente as 15 teclas do valor inicial do código, então nada as preenchia.
- **Por que os testes não pegaram:** as trocas de aba tinham sido testadas com outros tamanhos, nos quais a grade muda, e no Grande só depois de abrir o app, quando a resposta do PC preenche as teclas.
- **Correção:** tecla recém-criada é sempre preenchida. Nada se perdeu: os atalhos ficam no PC, e a tela é que ficava vazia.
- **Reprodução:** antes, 0 de 5 teclas ao voltar para Atalhos. Depois, 5 de 5.
- **Para não voltar:** `scripts/conferir_teclas.ps1` na skill, que confere as teclas ao trocar de aba, voltar de Ajustes e mudar um ajuste. Foi validado com uma compilação temporária com o erro de volta: o script acusou a falha, e com a versão corrigida passou.

## Versão 2.1.1: ícones na janela do PC

**Relato do usuário:** a lista de atalhos da janela do PC mostrava só os nomes, sem as imagens. O app do tablet não mudou; só o `AtalhosPC.exe`.

- **Causa:** ao abrir, o programa busca os ícones em segundo plano. Vindos do cache, eles chegam antes de a janela existir, e `OnUi` descartava o resultado sem aviso. Na primeira vez que um atalho entra, o ícone demora mais a ser gerado e chega depois, por isso o erro só aparecia ao reabrir o programa.
- **Correção:** `MainForm` cria o identificador da janela (handle) logo no início, então nada mais é descartado. Os atalhos, o cache e a configuração não foram alterados.
- **Efeito junto:** com o programa iniciado na bandeja, abrir o programa de novo não mostrava a janela, porque o pedido também era descartado. Agora mostra.
- **Reprodução:** antes, 3 de 3 aberturas sem ícones. Depois, 5 de 5 com os 5 ícones. No início pela bandeja, a janela apareceu com os ícones.
- **Para não voltar:** `ProtocolTest` monta a janela sem mostrá-la, com um ícone em cache, e confere se ele chega à lista. Com o erro de volta em uma cópia temporária, o teste falhou.

## Versão 2.2: biblioteca de sons

**Pedido:** uma biblioteca em Sons, dividida por categoria, para o usuário usar o som que quiser, copiada de uma pasta de sons que ele já tinha no PC.

- **Cópia:** 13.535 MP3 de 14 pastas foram para `dist/biblioteca/`. A pasta original não foi alterada. `sons.JPG`, uma captura da página do Myinstants, não foi copiada.
- **No PC:** botão **Biblioteca…** na aba Sons, com categorias e ícones, busca, ouvir, parar e adicionar vários de uma vez.
- **No aparelho:** **+ Adicionar** > **Biblioteca de sons**, com as mesmas funções e a lista em páginas. Essa é a versão 2.2 do app.
- **Ícones:** um por categoria, do Noto Emoji, sem logos de marcas. Cada som recebe uma cópia do ícone da categoria, que pode ser trocada.
- **Erro encontrado no tocador:** o teste com todos os arquivos mostrou 375 recusados pelo Windows, com o erro 277 do MCI. Eram MP3 válidos com etiquetas ID3v2 que o MCI não aceita, e sem a etiqueta 368 abriram. O tocador agora faz isso sozinho, usando uma cópia, e o erro valeria para qualquer MP3 do usuário.
- **Fora da biblioteca:** 7 arquivos sem áudio aproveitável continuam abrindo com erro no Windows, e só as cópias foram removidas: `kingdom of predators hxh`, `Zoldyck theme`, `Deltarune Menu Move`, `roblox bomb tick`, `Morse Code Dit`, `Undertale Move Selection Sound Effect` e `Rugby - NOSOUND`. Por isso são 13.528 sons.
- **Direitos:** os áudios vêm de uma coleção com memes, filmes, jogos e músicas de terceiros, sem licença conferida. Ficam só neste PC, fora da skill, do código-fonte e do Git (`.gitignore`).

## Versão 2.3: instalador e saudação do Cavaleiro AI

**Pedido:** o programa do Windows precisa ter um instalador, e a saudação do Cavaleiro AI precisa aparecer.

- **Instalador:** `dist/Instalar-AtalhosPC.exe`, de 1,7 MB, leva dentro o programa, o app e os sons de exemplo. É um programa próprio em C#, compilado pelo mesmo `build.ps1`, sem Inno Setup nem WiX, para o projeto continuar sem nada para instalar. O assistente tem quatro passos: Saudação, Opções, Instalação e Pronto.
- **Sem administrador:** instala em `%LOCALAPPDATA%\Programs\AtalhosPC`, a mesma pasta que o `instalar.ps1` da skill já usava, e cria os atalhos da área de trabalho e do menu Iniciar. Também registra o programa em **Aplicativos**, com `Desinstalar.exe`.
- **Atualizar e desinstalar sem perder nada:** o instalador nunca toca em `%APPDATA%\AtalhosPC`. A desinstalação apaga só o que está em `arquivos-instalados.txt`, ignora caminhos com `..` ou absolutos e só remove pastas vazias.
- **Fechar o programa sem matar:** o programa novo atende ao sinal `Local\AtalhosPC.Sair`, e o instalador o usa para fechá-lo como o **Sair** da bandeja, sem deixar ícone fantasma. Versões antigas, sem o sinal, são encerradas à força.
- **Biblioteca:** o instalador não leva os áudios. No assistente, ele acha a pasta `biblioteca` ao lado de si e oferece copiar, conferindo o espaço livre. Em modo silencioso, só copia com `/biblioteca`.
- **Saudação:** o texto do usuário, com acentos corrigidos ("é", "à", "sabedoria", "distribuída", "Só", "perfil"), nas cores da marca: fundo preto, destaque amarelo `#F2C230`. Ela é a primeira tela do instalador e aparece uma vez na primeira abertura do programa (`greeted` no `config.json`). Depois, o quadro preto no canto da janela a mostra de novo. O instalador abre o programa com `--saudacao-vista`, para não repetir. Clicar em `@cavaleiro.ai` ou em **Seguir no Instagram** abre o perfil.
- **Só no Windows:** o app do tablet continua na versão 2.2, sem mudança. O protocolo não mudou.
- **Skill:** `instalar.ps1` agora roda o instalador em modo silencioso, e a skill leva o `Instalar-AtalhosPC.exe`.

## Versão 2.4: escolher a pasta da biblioteca

**Problema relatado (06/11/2026):** em **Sons > + Adicionar > Biblioteca de sons**, o tablet não mostrava a biblioteca.

- **Causa:** o usuário tinha instalado o programa pelo instalador, sem copiar a biblioteca. O programa instalado procurava a pasta `biblioteca` só ao lado de si, em `%LOCALAPPDATA%\Programs\AtalhosPC`, e ela não existia: os 13.528 áudios continuavam em `dist\biblioteca`. Quem baixa o instalador do GitHub cai no mesmo caso, porque os áudios não vêm no download, e a única saída era rodar o instalador de novo ou copiar arquivos para dentro da pasta do programa.
- **Correção:** a janela **Biblioteca…** do PC ganhou **Escolher pasta…**. A pasta escolhida fica em `libraryDir`, no `config.json`, e vale na hora no PC e no aparelho, sem copiar nada. Uma pasta sem subpastas com áudio é recusada com explicação. Sem escolha, ou se a pasta escolhida sumir, o programa volta para a `biblioteca` ao lado do `.exe`, onde o instalador copia.
- **Mensagem:** a biblioteca vazia no PC passou a dizer o que fazer, antes do caminho, para não ser cortada.
- **Só no Windows:** o app continua na 2.2, e o protocolo não mudou.
- **No PC do usuário:** o programa instalado foi atualizado para a 2.4, com os mesmos 21 atalhos e 25 sons, e a pasta escolhida é `dist\biblioteca`. No tablet, a biblioteca abriu com as 14 categorias.
- **Testes:** 96 do protocolo, 6 deles novos (pasta escolhida, pasta que sumiu, configuração guardada e troca na mesma conexão), e 56 do instalador.

## Skill

`.claude/skills/atalhos-pc/` traz tudo para repetir a instalação em outro aparelho, mesmo copiada para outro computador:

| Parte | Conteúdo |
|---|---|
| `SKILL.md` | Roteiro completo, regras de conduta e a mensagem exibida em toda utilização. |
| `assets/` | Instalador, programa, app, sons de exemplo, ícones das categorias da biblioteca, código-fonte sem a chave de assinatura e hashes. A biblioteca de 1,6 GB não entra. |
| `scripts/` | `diagnostico.ps1`, `instalar.ps1`, `conferir_teclas.ps1`, `medir_som.ps1`, `tocar_ui.ps1`. |
| `references/` | Aparelhos, problemas, mapa do app, recompilação, guia e arquitetura. |

Depois de alterar o app, rode `assets\empacotar_skill.ps1` para atualizar a skill.

**Testes da skill:** diagnóstico com um e com dois aparelhos; instalação recusada com o programa aberto e sem aparelho definido; arquivo adulterado detectado pelo hash; cópia da skill fora do projeto funcionando; instalação em pasta nova com cópia de segurança da configuração; toque por texto na tela; medição de som (`SILENCIO` e `SOM DETECTADO`); arquivo de teste aberto no PC pelo roteiro; código-fonte do pacote compilado do zero, com 60 testes aprovados. Nenhum desses testes usou outro aparelho além do tablet.

**Testes da skill na versão 2.3:** `instalar.ps1` recusou com o programa aberto e sem `-Substituir`, sem mexer em nada. Com `-Substituir`, instalou numa pasta com espaço no nome, copiou uma biblioteca de exemplo também com espaço no nome, criou o atalho e a entrada em **Aplicativos** e guardou a cópia da configuração. O comando de desinstalação registrado limpou tudo. O código do pacote foi compilado do zero, seguindo `recompilar.md`, com 90 e 56 testes aprovados.

## Como foi testado

Bancada: Galaxy Tab A8 (SM-X200, Android 14) e Windows 10 Pro.

| Item | Resultado |
|---|---|
| Testes automáticos do protocolo, ataques, sons, biblioteca e ícones da janela do PC | 90 aprovados, 0 falhas |
| Testes automáticos do instalador, em pastas e chaves de teste | 56 aprovados, 0 falhas: conteúdo igual ao de `dist`, cópia, atalhos, entrada em Aplicativos, iniciar com o Windows, atualizar por cima, arquivos do usuário preservados, caminhos perigosos recusados, assistente clicado do começo ao fim e saudação |
| Instalador de verdade, na pasta padrão, com dados de teste | Instalou com o programa aberto, que fechou pelo sinal; a cópia instalada abriu, escutou na porta 47810 e não repetiu a saudação. Atualizou por cima com ela aberta e desinstalou pelo `QuietUninstallString`, que fechou o programa pelo sinal e apagou pasta, atalhos e entrada. Os três fechamentos foram sem forçar |
| Saudação na janela do PC | Abriu sozinha na primeira abertura, não repetiu na segunda nem depois do instalador, e o quadro a abriu de novo. Conferida por captura de tela, junto com as quatro telas do instalador |
| Programa 2.3 com os dados reais do usuário | Abriu com os mesmos 21 atalhos, 25 sons e 1 aparelho, e mostrou a saudação |
| Todos os áudios da biblioteca abertos pelo Windows, sem tocar | 13.160 de primeira; dos 375 recusados, 368 abriram pelo tocador corrigido e 7 saíram da biblioteca |
| Janela da biblioteca aberta pelo botão real, com dados temporários | 15 itens de categoria, contagens certas, busca `gato` (26), `ÁGUA` (16) e sem resultado (0) |
| Ouvir na janela do PC, medido por fora | Pico 0,478; depois de **Parar**, 0,000 |
| Adicionar duas vezes o mesmo som | Entra uma vez, com o ícone da categoria e o áudio na biblioteca |
| Biblioteca no tablet | Categoria certa ao tocar (3 de 3), busca `gato` (26), voltar e sair. O próprio usuário adicionou sons durante os testes |
| Ouvir pelo tablet, medido por fora | Pico 0,478; depois de **Parar** e ao sair da tela, 0,000 |
| Lista além da primeira página no tablet | Rolagem carregou mais sons, sem erro do app |
| Teclas depois da versão 2.2, com `conferir_teclas.ps1` | `RESULTADO: teclas ok`, com 18 atalhos na página e 25 sons |
| USB por Ancoragem e por adb | Funcionam |
| Do toque ao programa existir no PC | 331 a 455 ms com o app aberto; 735 ms com ele fechado |
| Reconexão depois de fechar o programa | 4 s |
| Atalho na tela inicial, para arquivo e para som | Funciona |
| Som saindo no PC, medido por fora do app | Pico 0,372 com o som em 80% |
| Fader do som em 40% | Pico 0,092 |
| Fader geral em cerca de 50% | Pico 0,092 |
| Mudo | Pico 0,000 |
| Parar | Pico 0,000 |
| Dois sons juntos | Pico 0,660 |
| 10 temas, 4 tamanhos, só logo, busca, páginas, ícone por emoji | Conferidos por captura de tela |
| Teclas na tela após trocar de aba, ir a Ajustes (sem mudar, mudando nomes e tema), sair e voltar, girar e reabrir | 15 caminhos, com contagem de teclas: todas presentes |
| Compilação com ferramentas baixadas do zero, com hash conferido | Funciona |

Os tempos incluem o início do comando de teste no aparelho.

## O que não foi testado

| Item | Situação |
|---|---|
| **Wi-Fi** | O PC está no cabo em `192.168.10.x` e o tablet no Wi-Fi em `192.168.0.x`, e o tablet não alcança o PC. Usa o mesmo código da Ancoragem USB. |
| **Bluetooth** | O PC não tem adaptador. Só foi verificado que o programa detecta a falta e segue funcionando, e o layout das estruturas nativas. |
| **Celular real** | O layout estreito foi simulado no tablet. |
| **Outras versões do Android** | Compila para Android 5.0+, mas só o Android 14 foi executado. |
| **Outros formatos de áudio** | Só MP3 foi tocado. WAV, WMA, M4A e AAC dependem dos codecs do Windows. |
| **Cada som da biblioteca** | Todos abrem, mas só alguns foram tocados. Não foi conferido de ouvido se cada som corresponde ao nome. |
| **Instalar a biblioteca em outro PC** | `instalar.ps1 -SoBiblioteca` foi testado neste PC, com uma pasta pequena. |
| **Qualidade dos sons de exemplo** | O nível foi medido. Falta conferir de ouvido se cada som corresponde ao nome. |
| **Instalador em outro PC** | Só rodou neste Windows 10. Falta Windows 11, um PC sem o programa e o aviso do SmartScreen em um arquivo baixado. |
| **Instalador fechando uma versão antiga** | Encerrar à força um programa sem o sinal (até a 2.2) não foi testado de ponta a ponta, porque abriria mais uma pergunta do firewall na tela do usuário. |
| **Instalador com a biblioteca inteira** | A cópia foi testada com bibliotecas pequenas. Os 13.528 arquivos (1,6 GB) não foram copiados pelo instalador. |
| **Instalar como administrador ou em `Program Files`** | Não testado. A pasta padrão é do usuário e não precisa disso. |

## Limites conhecidos

- O app é só para Android, e o programa só para Windows. iPhone, iPad, Mac e Linux não funcionam.
- O som sai na saída padrão do Windows, sem escolha dentro do app.
- Os sons precisam estar no PC: o app não envia áudio guardado no aparelho.
- O medidor mostra toda a saída do PC, inclusive outros programas.
- O programa do PC precisa estar aberto. O app não acorda um PC desligado.
- O app não fica conectado em segundo plano nem mantém a tela ligada.
- Quem está pareado pode abrir qualquer arquivo do PC. Desligar **Permitir navegar** restringe à lista.
- O programa e o instalador não têm assinatura digital, então o SmartScreen pode avisar em arquivos baixados.
- Cada pasta nova de onde o programa roda faz o firewall do Windows perguntar uma vez.

## Problemas encontrados e como foram resolvidos

| Problema | Causa | Solução |
|---|---|---|
| Efeito da tecla não aparecia | Tablet com animações em 0x anula o `ValueAnimator` | Animação por relógio próprio |
| `build.ps1` falhava à toa | Avisos no stderr viram erro no PowerShell 5.1 com `Stop` | `Continue` e conferência de `$LASTEXITCODE` |
| Firewall aparecia como "falta liberar" | Só procurava a regra com o nome do programa | Procura qualquer regra que permita aquele `.exe` |
| `Voltar` subia por pastas do sistema | Navegava pela pasta-pai | Histórico do que foi visitado |
| Download automático recusado (403) no BigSoundBank | O site não permite | Não foi contornado: troca pelo Wikimedia Commons |
| Commons respondeu 429 | Muitos pedidos seguidos | Pausas entre os pedidos |
| Layout de celular apertado | Visor encostava nos botões | Textos curtos e faixa em duas linhas |
| Teclas sumiam ao trocar de aba ou voltar de Ajustes | Grade nova não era preenchida quando mantinha o tamanho | Tecla recém-criada é sempre preenchida (versão 2.1) |
| Janela do PC sem as imagens dos atalhos ao reabrir | Ícones do cache chegavam antes de a janela existir e eram descartados | A janela cria o handle logo no início (versão 2.1.1) |
| 375 MP3 válidos recusados pelo Windows | O MCI não aceita certas etiquetas ID3v2 (erro 277) | O tocador abre uma cópia sem a etiqueta, e o original não muda (versão 2.2) |
| Clique simulado não abria a biblioteca nos testes | O WinForms só conta o clique se o botão estiver visível na frente | Teste passou a abrir as janelas reais e apertar os botões por dentro |
| Categorias abriam sozinhas durante o teste no tablet | Era o usuário usando o tablet ao mesmo tempo: `getevent` mostrou 232 toques físicos em 15 s | Nenhum defeito. Antes de testar por toque simulado, confirme que ninguém está usando o aparelho |
| Script tocava na legenda, não no pad | O nome do som selecionado aparece duas vezes na tela | Prefere o item que aceita toque |
| Acento se perdia na linha de comando | Codificação do PowerShell 5.1 | Busca também por parte do texto, e não toca se houver mais de um resultado |
| App ficava fechado após instalar | A instalação encerra o app | O script reabre o app |
| Teste do instalador ficou parado para sempre | `Start-Process -Wait` do PowerShell 5.1 espera também os filhos, e o programa aberto por `/abrir` continua rodando. O instalador já tinha terminado em menos de 1 s | Esperar só o processo do instalador (`-PassThru` e `.WaitForExit()`), também no `instalar.ps1` (versão 2.3) |
| Saudação modal travava o teste da janela | `ShowDialog` roda um laço próprio, e o teste esperava dentro dele | O teste inspeciona e fecha a saudação por `Invoke` de outra thread |
| Biblioteca vazia no tablet depois de instalar | O programa instalado só procurava `biblioteca` ao lado de si, e o instalador não a copiou | **Escolher pasta…** na janela da biblioteca, guardada no `config.json` (versão 2.4) |

## Arquivos

| Caminho | Conteúdo |
|---|---|
| `README.md` | Visão geral |
| `docs/GUIA-DE-USO.md` | Manual de quem usa |
| `docs/COMO-FUNCIONA.md` | Arquitetura, protocolo, segurança e compilação |
| `docs/O-QUE-FOI-FEITO.md` | Este registro |
| `dist/` | `Instalar-AtalhosPC.exe`, `AtalhosPC.exe`, `AtalhosPC.apk`, `sons/` e `biblioteca/` |
| `android/`, `windows/` | Código-fonte. O instalador fica em `windows/setup/` |
| `assets/` | Logo, ícones das categorias e scripts de ícones, sons e empacotamento |
| `.claude/skills/atalhos-pc/` | A skill |

A chave `android/atalhospc.keystore` e sua senha ficam só nesta pasta e não devem ser publicadas.
