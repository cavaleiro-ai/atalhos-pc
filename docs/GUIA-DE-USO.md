# Guia de uso do Atalhos PC

## 1. Instalar

### No computador, com o instalador (recomendado)

1. Dê dois cliques em `Instalar-AtalhosPC.exe`. Se ele veio da internet, o SmartScreen pode avisar: clique em **Mais informações** e **Executar assim mesmo**. Ele não pede senha de administrador.
2. A primeira tela é a **saudação do Cavaleiro AI**. Clicar em **@cavaleiro.ai** abre o perfil no Instagram. Clique em **Avançar**.
3. Na tela **Onde e como instalar**:
   - **Pasta do programa:** deixe a sugerida, `%LOCALAPPDATA%\Programs\AtalhosPC`, que é do seu usuário. Se escolher outra, o programa vai para uma subpasta `AtalhosPC` dentro dela.
   - **Atalhos:** área de trabalho e menu Iniciar vêm marcados.
   - **Iniciar junto com o Windows:** o programa abre escondido perto do relógio toda vez que o PC liga, pronto para o tablet.
   - **Biblioteca de sons (opcional):** se a pasta `biblioteca` estiver ao lado do instalador, ele já a encontra e mostra quantos sons há. Senão, clique em **Escolher…** e aponte uma pasta com uma subpasta por categoria. Sem biblioteca, o programa funciona igual.
   - Se o Atalhos PC estiver aberto, um aviso diz que ele será fechado. Ele fecha sozinho e abre de novo no fim.
4. Clique em **Instalar**. Sem a biblioteca, leva menos de um segundo; com milhares de sons, alguns minutos.
5. Na tela **Pronto!**, deixe **Abrir o Atalhos PC agora** marcado e clique em **Concluir**.
6. Quando o Windows perguntar sobre o acesso à rede, clique em **Permitir acesso**. Sem isso, Wi-Fi e Ancoragem USB não funcionam. Se o aviso não aparecer, use o botão **Liberar no firewall do Windows** na janela do programa.

O instalador coloca na pasta: o programa, o `AtalhosPC.apk` para o tablet, os sons de exemplo e o `Desinstalar.exe`. Seus atalhos, sons e aparelhos pareados ficam em `%APPDATA%\AtalhosPC`, fora da pasta do programa: instalar de novo, atualizar ou desinstalar nunca os apaga.

**Atualizar:** rode o instalador da versão nova. Ele acha a pasta da instalação anterior, fecha o programa, troca os arquivos e mantém tudo seu.

### No computador, sem instalar

Copie `AtalhosPC.exe` e a pasta `sons` juntos para uma pasta fixa e dê dois cliques no `.exe`. Se mudar o arquivo de lugar depois, será preciso liberar o firewall de novo. Nesse modo não há atalhos nem entrada em **Aplicativos**. Marque **Iniciar junto com o Windows** na janela do programa para ele ficar sempre disponível.

### A saudação do Cavaleiro AI

Ela aparece na primeira tela do instalador. Quem abre o programa sem instalar a vê uma vez, na primeira abertura. Depois, o quadro preto **Saudações, cavaleiros e caveiras.**, embaixo à direita na janela do PC, mostra a saudação de novo, com o botão **Seguir no Instagram**.

Fechar a janela no X não encerra o programa, ele continua na bandeja. Para sair de vez, clique com o botão direito no ícone da bandeja e em **Sair**.

### No tablet ou celular

O `AtalhosPC.apk` fica na pasta do programa (o botão **Abrir a pasta do programa**, no fim do instalador, leva até ela). Copie-o para o aparelho e toque nele pelo app de arquivos. O Android vai pedir para autorizar a instalação por aquele app. Quem usa depuração USB também pode instalar pelo PC:

```
adb install -r AtalhosPC.apk
```

## 2. Conectar

O app usa o modo **Automático**: tenta o último caminho que funcionou, depois USB, depois Wi-Fi e por fim Bluetooth. Para fixar um deles, toque no visor de estado no canto superior esquerdo e escolha em **Conectar por**.

Até agora, só as duas formas de USB foram testadas de ponta a ponta. Wi-Fi e Bluetooth estão prontos no app, mas ainda não foram comprovados.

### USB (caminho testado)

Ligue o cabo e ative a **Ancoragem USB** no aparelho: `Configurações > Conexões > Roteador Wi-Fi e Ancoragem > Ancoragem USB`. O nome muda entre marcas; o botão **Abrir Ancoragem USB** no app leva direto à tela quando o aparelho permite.

Ligar a ancoragem também faz o PC poder usar a internet do aparelho. Em celular com plano de dados, isso pode consumir a franquia. Nesse caso, prefira a opção abaixo.

**Alternativa para quem usa depuração USB:** marque **USB por adb** na janela do PC. Precisa do `adb.exe` no PATH, ao lado do programa ou na pasta do Android SDK. Ela não usa a internet do aparelho nem depende do firewall.

### Wi-Fi

O PC e o aparelho precisam estar **na mesma rede**, no mesmo roteador. Um PC no cabo de um roteador e um tablet no Wi-Fi de outro não se enxergam, mesmo com internet funcionando nos dois. Rede de convidados e isolamento de clientes no roteador também bloqueiam.

Para conferir, compare os IPs: a janela do PC mostra o dele, e os três primeiros números costumam ser iguais aos do aparelho, como `192.168.0.10` e `192.168.0.9`. Se o PC não aparecer na lista, digite o IP em **Digitar o IP do PC**.

### Bluetooth

O PC precisa ter adaptador Bluetooth. A janela do PC mostra `Bluetooth: Ativo` quando está pronto; se aparecer `Sem adaptador disponível`, ele não encontrou nenhum. Um adaptador USB ligado depois é detectado em até 30 segundos.

1. Pareie o aparelho com o PC nas configurações de Bluetooth de um dos dois.
2. No app, abra a tela de conexão e toque em **Mostrar PCs pareados por Bluetooth**. Permita o acesso quando o Android pedir.
3. Toque no nome do PC.

## 3. Parear

Feito uma vez para cada aparelho. É o que impede que outra pessoa na mesma rede abra arquivos no seu PC.

1. No PC, clique em **Parear novo aparelho**. Aparece um código como `2S2-5M2`, válido por 3 minutos.
2. No app, toque em **Conectar ao PC**, escolha o PC na lista e digite o código.

O código vale para um único aparelho. Cinco tentativas erradas o cancelam, e aí basta gerar outro.

## 4. Montar o deck

**Pelo PC:** arraste arquivos, pastas ou atalhos para a lista na janela, ou use **Adicionar arquivos** e **Adicionar pasta**.

**Pelo aparelho:** toque em uma tecla vazia ou em **+ Adicionar**, navegue nas pastas do PC e toque no item. Dá para abrir na hora ou adicionar às teclas.

Segure uma tecla para ver as opções:

| Opção | O que faz |
|---|---|
| Renomear | Muda o nome mostrado na tecla. |
| Mover para a esquerda, direita ou início | Organiza as teclas. |
| Criar atalho na tela inicial | Coloca um ícone na tela inicial do Android que abre o arquivo direto. |
| Remover da lista | Tira a tecla. O arquivo continua no PC. |

Quando os atalhos não couberem na página, deslize para o lado. O botão **Buscar** aparece a partir de 13 itens. O app ocupa a tela inteira; deslize da borda para ver as barras do Android.

## 5. Área de Sons

Toque em **Sons** no topo. Cada pad toca um som **nas caixas do PC, não no aparelho**. Os 11 sons de exemplo entram sozinhos na primeira vez.

| Controle | O que faz |
|---|---|
| Tocar um pad | Toca o som e seleciona o pad. Tocar de novo recomeça. Sons diferentes podem tocar juntos. |
| Fader **GERAL** | Volume de todos os sons. Não muda o volume do Windows nem de outros programas. |
| Fader da direita (nome do som) | Volume só do pad selecionado. A barrinha no pad mostra esse volume. |
| Medidor entre os faders | Nível real que está saindo no PC, incluindo outros programas. |
| **Mudo** | Silencia os sons do app, mesmo os que já estão tocando. Aceso, o botão mostra **Sem som**. |
| **Parar** | Interrompe todos os sons na hora. |

**Adicionar um som:** toque em um pad vazio ou em **+ Adicionar**, navegue até o áudio no PC e escolha um ícone. No PC, arraste o arquivo para a aba **Sons**. Use MP3, que é o formato testado; WAV, WMA, M4A e AAC dependem do Windows.

### Biblioteca de sons

A biblioteca é uma pasta de áudios que fica no PC, separada em categorias, com busca e prévia. Os sons só entram nos seus pads quando você os adiciona.

**Os áudios da biblioteca não vêm no download.** Quem baixa recebe só os 11 sons de exemplo, que têm licença livre. A biblioteca é montada com os seus próprios sons:

1. Crie uma pasta em qualquer lugar do PC, como `Músicas\Minha biblioteca`.
2. Dentro dela, crie uma subpasta por categoria, como `Memes` ou `Efeitos sonoros`, e copie os áudios para elas. Só os arquivos que ficam direto na subpasta contam.
3. Para dar um ícone à categoria, coloque um PNG com o mesmo nome da subpasta ao lado dela, como `Minha biblioteca\Memes.png`. A pasta `assets/icones-biblioteca` do repositório tem 14 ícones prontos.
4. No PC, abra a aba **Sons**, clique em **Biblioteca…** e depois em **Escolher pasta…**. Escolha a pasta de cima, a que tem as subpastas. A biblioteca aparece na hora, no PC e no aparelho, e o programa lembra a pasta nas próximas vezes.

Outra forma é deixar o instalador copiar a pasta para junto do programa, em **Copiar a biblioteca de sons**. Ele copia os áudios e completa os ícones que faltarem. Sem pasta escolhida, o programa procura uma pasta `biblioteca` ao lado do `AtalhosPC.exe`, que é onde o instalador copia. Se a pasta escolhida sumir, como num HD externo desligado, ele volta a procurar ao lado do programa.

O programa traduz 14 nomes de pasta em inglês: `Anime & Manga`, `Games`, `Memes`, `Movies`, `Music`, `Politics`, `Pranks`, `Reactions`, `Sound Effects`, `Sports`, `Television`, `Tiktok Trends`, `Viral` e `Whatsapp Audios`. Qualquer outro nome aparece como está.

Só use áudios que você tem direito de usar.

**No PC:**

1. Abra a aba **Sons** e clique em **Biblioteca…**.
2. Escolha uma categoria à esquerda, ou **Todas**.
3. Para procurar, digite em **Buscar**. Maiúsculas e acentos não importam, e as palavras podem vir em qualquer ordem: `agua` acha `Água`.
4. Clique duas vezes em um som para ouvir. **Parar** interrompe.
5. Selecione um ou mais sons (`Ctrl` ou `Shift` + clique) e clique em **Adicionar aos meus sons**, ou aperte `Enter`.

**No aparelho:**

1. Em **Sons**, toque em **+ Adicionar** ou em um pad vazio, e depois em **Biblioteca de sons**.
2. Toque em uma categoria, ou digite em **Buscar som** para procurar em todas.
3. **Ouvir** toca o som nas caixas do PC, e **Parar**, no alto, interrompe. Sair da tela também para o som.
4. **Adicionar** coloca o som nos seus pads. O botão muda para **Na lista**.

Cada som entra com o ícone da categoria e com volume de 80%. Depois, ele funciona como qualquer outro pad: segure para trocar o ícone, renomear ou ajustar o volume. Remover um pad não apaga o som da biblioteca, e você pode adicioná-lo de novo.

A prévia usa o volume geral, então nada se ouve com **Mudo** ligado. A biblioteca continua disponível com **Permitir navegar** desligado, porque só alcança a própria pasta.

**Colocar mais sons:** crie ou use uma subpasta dentro da pasta da biblioteca e copie os áudios para ela. A pasta vira uma categoria com o próprio nome. Um PNG com o mesmo nome da pasta, ao lado dela, vira o ícone. Os sons novos aparecem na próxima vez que você abrir a biblioteca.

Segure um pad para **ajustar o volume** sem tocar, **trocar o ícone** (emoji ou imagem do PC), renomear, mover, criar um atalho na tela inicial ou remover. Remover não apaga o arquivo do PC.

## 6. Temas, tamanho e nomes

Toque em **Ajustes**. Tudo vale na hora e fica guardado só naquele aparelho.

- **Tema:** 10 opções, de preto puro a claro. Os pads mudam de cor junto.
- **Tamanho dos botões:** Grande, Médio, Pequeno ou Mínimo, separado para Atalhos e Sons. Em Atalhos, deitado, vai de 15 a 60 botões por página.
- **Mostrar o nome no botão:** desligado, fica só a logo centralizada. Também é separado para cada área.

## 7. O que as cores dizem

| Sinal | Significado |
|---|---|
| Luz verde no visor | Conectado. O visor mostra o caminho: USB, USB (adb), Wi-Fi, Rede ou Bluetooth. |
| Luz amarela | Conectando. |
| Luz vermelha, "Sem conexão" | Não encontrou o PC. As teclas continuam visíveis e, ao tocar em uma, o app espera até 8 segundos pela conexão. |
| Luz vermelha, "Parear de novo" | O aparelho foi removido da lista no PC. |
| Tecla acende em verde | O arquivo abriu no PC. |
| Tecla acende em vermelho | Não abriu. A mensagem explica o motivo. |
| Tecla apagada | O arquivo não existe mais naquele caminho. |
| Pad com aro branco pulsando | O som está tocando. |
| Barrinha no pad | Volume daquele som. |

## 8. Problemas comuns

| Sintoma | O que fazer |
|---|---|
| O PC não aparece na lista | Veja se o programa está aberto e se a janela mostra `Firewall: liberado`. Confira se a Ancoragem USB está ligada ou se os dois estão na mesma rede. |
| A janela mostra `Firewall: falta liberar` | Clique em **Liberar no firewall do Windows** e confirme a pergunta do Windows. |
| Funciona no USB e não no Wi-Fi | Quase sempre são redes diferentes. Compare os IPs, como explicado na seção de Wi-Fi. |
| O PC está numa rede marcada como Pública e não conecta | A regra criada pelo aviso do Windows pode valer só para redes Particulares. Use o botão do firewall no programa, que libera a sub-rede local em qualquer perfil. |
| `Parear de novo` | Gere um código no PC e pareie outra vez. |
| As teclas somem ao trocar de aba ou mudar algo em Ajustes | Era um erro da versão 2.0. Instale a versão atual do app. Seus atalhos não se perdem, pois ficam no PC. |
| A biblioteca aparece vazia, ou o app diz que ela não está instalada | O programa não achou a pasta dos sons. No PC, abra **Sons > Biblioteca… > Escolher pasta…** e escolha a pasta com uma subpasta por categoria. Isso vale na hora, também no aparelho. Precisa do programa 2.4 ou mais novo; antes dele, a pasta `biblioteca` tinha de ficar ao lado do `AtalhosPC.exe`. |
| O instalador diz que não conseguiu fechar o Atalhos PC | Feche pelo ícone perto do relógio (botão direito, **Sair**) e rode o instalador de novo. |
| O instalador diz que falta espaço | A biblioteca precisa do mesmo espaço que ocupa na pasta de origem, mais 50 MB. Libere espaço ou instale sem a biblioteca. |
| O instalador diz que não tem permissão na pasta | A pasta escolhida é protegida, como `C:\Program Files`. Use a pasta sugerida. |
| Qualquer outro erro do instalador | O motivo aparece na tela final. O registro completo fica em `%TEMP%\AtalhosPC-instalador.log`. Nada dos seus atalhos e sons é apagado. |
| A biblioteca avisa para atualizar o programa do PC | O app é mais novo que o programa. Instale o `AtalhosPC.exe` atual. |
| A janela do PC mostra só os nomes dos atalhos, sem as imagens | Era um erro do programa até a versão 2.1. Instale o `AtalhosPC.exe` atual. Nada se perde, e o tablet continua igual. |
| O programa abre atrás de outras janelas | É uma proteção do Windows contra roubo de foco. O programa abriu; clique nele na barra de tarefas. |
| O programa pede confirmação de administrador | A confirmação aparece na tela do PC. Alguém precisa estar lá para aceitar. |
| O pad não toca e mostra erro | O Windows não abriu o formato. Converta para MP3. |
| O medidor mexe, mas não ouço nada | Confira a saída de áudio padrão e o volume do Windows, e se o botão Mudo do app está aceso. |
| O medidor mexe sem eu tocar nada | Ele mostra toda a saída do PC, inclusive música e vídeo de outros programas. |
| A instalação do APK é bloqueada | Autorize a instalação pelo app de arquivos. Em aparelhos Xiaomi, ative também "Instalar via USB" nas opções do desenvolvedor para usar `adb install`. |

## 9. Remover

- **Tirar o acesso de um aparelho:** no PC, selecione-o em **Aparelhos pareados** e clique em **Remover aparelho selecionado**. Ele perde o acesso na hora.
- **Desinstalar do PC (instalado pelo instalador):** `Configurações > Aplicativos`, procure **Atalhos PC** e clique em **Desinstalar**. Também dá para abrir o `Desinstalar.exe` da pasta do programa. Ele fecha o programa, apaga só o que o instalador colocou (arquivos seus na pasta ficam), remove os atalhos e a opção de iniciar com o Windows.
- **Apagar também seus atalhos, sons e pareamentos:** depois de desinstalar, apague a pasta `%APPDATA%\AtalhosPC`. O desinstalador não faz isso de propósito, para uma reinstalação voltar como estava.
- **Desinstalar do PC (sem instalador):** desmarque **Iniciar junto com o Windows**, saia pela bandeja e apague o `.exe` e a pasta `sons`.
- **Regra do firewall:** sai em `Firewall do Windows com Segurança Avançada > Regras de Entrada`.
- **Desinstalar do aparelho:** desinstale o app normalmente.
