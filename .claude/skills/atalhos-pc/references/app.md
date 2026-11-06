# Mapa do app e do programa

Os textos abaixo servem também como alvos de `scripts\tocar_ui.ps1 -Texto`. Teclas e pads respondem pelo nome do item, e os vazios por `Tecla vazia: adicionar`.

## Tela principal do app

| Elemento | Texto | O que faz |
|---|---|---|
| Visor de estado | caminho e nome do PC | Abre a tela de conexão. Luz verde conectado, amarela conectando, vermelha sem conexão. |
| Seletor | `Atalhos`, `Sons` | Troca de área. |
| Buscar | `Buscar` (`Busca` em tela estreita) | Aparece a partir de 13 itens. |
| Adicionar | `+  Adicionar` (`+` em tela estreita) | Navega nos arquivos do PC. |
| Ajustes | `⚙  Ajustes` (descrição `Ajustes` em tela estreita) | Tema, tamanho e nomes. |

**Atalhos:** um toque abre; a tecla acende em verde quando abriu e em vermelho com erro. Deslize para o lado para trocar de página.

**Sons:**

| Elemento | O que faz |
|---|---|
| Pad | Toca e seleciona. Tocar de novo recomeça. Sons diferentes tocam juntos, até 32. Enquanto toca, ganha um aro branco pulsando. |
| Fader `GERAL` | Volume de todos os sons do app, sem mexer no volume do Windows. |
| Fader com o nome do som | Volume só do pad selecionado. |
| Medidor | Pico real da saída de áudio do PC, de todos os programas. |
| `Mudo` | Silencia os sons do app; aceso, mostra `Sem som`. |
| `Parar` | Interrompe todos os sons. |

**Menu ao segurar:**

| Opção | Atalhos | Sons |
|---|---|---|
| `Ajustar volume` (seleciona sem tocar) | | ✓ |
| `Trocar ícone` (64 emojis ou imagem do PC) | | ✓ |
| `Renomear` | ✓ | ✓ |
| `Mover para a esquerda`, `direita`, `início` | ✓ | ✓ |
| `Criar atalho na tela inicial` (Android 8+) | ✓ | ✓ |
| `Remover da lista` (não apaga o arquivo) | ✓ | ✓ |

## Primeira abertura e navegação

Sem PC pareado, a tela principal mostra o botão `Conectar ao PC`. Ao tocar em um PC da lista, o app pede o código em `Parear com NOME`; o botão de confirmar é `Parear`.

A navegação (`+  Adicionar` ou tecla vazia) começa em `Locais do PC`: `Área de Trabalho`, `Programas do Menu Iniciar`, `Programas (todos os usuários)`, `Documentos`, `Downloads`, `Imagens`, `Músicas`, `Vídeos` e os discos. `Voltar` retorna ao que foi visitado.

| Aberta a partir de | Mostra | Ao tocar em um arquivo |
|---|---|---|
| Atalhos | Tudo | `Abrir no PC` ou `Adicionar aos atalhos` |
| Sons | Pastas e áudios | Adiciona e abre a escolha de ícone |

Na área de Sons, **+ Adicionar** e o pad vazio perguntam **Biblioteca de sons** ou **Arquivo do PC**. Com **Permitir navegar** desligado, vão direto à biblioteca. Na biblioteca: toque em uma categoria, ou busque em **Buscar som**; `Ouvir` toca no PC, `Adicionar` vira `Na lista`, `Parar` e `Voltar` ficam no alto. `Voltar` limpa a busca e depois retorna às categorias.
| Trocar ícone > `Imagem do PC` | Pastas e imagens | Usa como ícone |

## Ajustes

Ficam guardados só naquele aparelho.

**Temas** (alvos `Tema Grafite` e assim por diante):

| Tema | Estilo |
|---|---|
| Grafite | Metal escuro discreto |
| Preto Absoluto | Preto puro, ideal para telas AMOLED |
| RGB Gamer (padrão) | Luz da borda em todas as cores |
| Neon Cyber | Magenta e ciano sobre violeta |
| Matrix | Verde fósforo sobre preto |
| Tron | Azul elétrico sobre marinho |
| Aurora | Verde água e violeta em degradê |
| Vulcão | Brasa, laranja e carvão |
| Ouro Negro | Preto com detalhes dourados |
| Gelo | Claro, prata e azul |

**Tamanho dos botões**, separado para Atalhos e Sons. Colunas no lado comprido da tela; as linhas se ajustam à área livre.

| Tamanho | Atalhos, deitado | Sons, deitado | Em pé |
|---|---|---|---|
| Grande | 5 colunas (15 botões em tablet) | 4 colunas | 3 colunas |
| Médio | 6 | 5 | 4 |
| Pequeno | 8 | 7 | 5 |
| Mínimo | 10 (60 botões em tablet) | 9 | 6 |

**Mostrar o nome no botão:** desligado, fica só a logo centralizada. Também é separado por área.

## Tela de conexão

`PCs pareados` (segure para esquecer), `Conectar por` (`Automático`, `USB`, `Wi-Fi`, `Bluetooth`), `PCs encontrados na rede e no USB`, `Procurar de novo`, `Abrir Ancoragem USB`, `Digitar o IP do PC`, `Conectar neste IP`, `Mostrar PCs pareados por Bluetooth`, `Abrir configurações de Bluetooth`.

No modo Automático, a ordem é: último caminho que funcionou, túnel adb, redes encontradas com USB antes do Wi-Fi, IPs salvos e Bluetooth.

## Janela do programa no PC

| Área | Conteúdo |
|---|---|
| Aba **Atalhos** | Arraste arquivos, ou use `Adicionar arquivos…`, `Adicionar pasta…`, `Abrir`, `Renomear`, `Remover`, `◀ Mover`, `Mover ▶`. |
| Aba **Sons** | `Biblioteca…` abre as categorias, a busca, `Ouvir`, `Parar` e `Adicionar aos meus sons`. Arraste áudios, ou use `Adicionar sons…`, `Tocar`, `Parar tudo`, `Ícone…`, `Renomear`, `Remover`, mover, mais os volumes do som e geral. |
| **Parear tablet ou celular** | `Parear novo aparelho` mostra o código por 3 minutos. |
| **Conexões** | Porta, IPs, Bluetooth, firewall, túnel adb e quem está conectado. |
| **Aparelhos pareados** | `Remover aparelho selecionado` corta o acesso na hora. |
| **Opções** | `Iniciar junto com o Windows`, `Permitir navegar nos arquivos pelo aparelho`, `USB por adb (depuração USB)`, `Liberar no firewall do Windows`. |
| Quadro preto **Saudações, cavaleiros e caveiras.** | Abre a saudação do Cavaleiro AI, com `Seguir no Instagram` e `Fechar`. Na primeira abertura do programa neste PC, ela aparece sozinha uma vez. |

Fechar no X só esconde na bandeja. Para sair: botão direito no ícone da bandeja > `Sair`. O instalador e o desinstalador fecham o programa sozinhos, pelo mesmo caminho do `Sair`.

## Instalador no PC

`Instalar-AtalhosPC.exe`: **Saudação** (texto do Cavaleiro AI; `@cavaleiro.ai` abre o Instagram) > **Opções** (`Pasta do programa`, `Escolher…`, `Criar atalho na área de trabalho`, `Criar atalho no menu Iniciar`, `Iniciar junto com o Windows`, `Copiar a biblioteca de sons para a pasta do programa`) > `Instalar` > **Pronto!** (`Abrir a pasta do programa`, `Abrir o Atalhos PC agora`, `Concluir`). Desinstalar: `Configurações > Aplicativos > Atalhos PC`, ou `Desinstalar.exe` na pasta do programa.

**Permitir navegar**, desligado, faz o aparelho usar só o que já está na lista: não navega, não adiciona e não abre caminhos soltos.

## Onde ficam os dados no PC

`%APPDATA%\AtalhosPC\`: `config.json`, `atalhos.log`, `icones\`, `icones-sons\`, `sons\`. O programa instalado fica em `%LOCALAPPDATA%\Programs\AtalhosPC\`, e o registro do instalador, em `%TEMP%\AtalhosPC-instalador.log`.
