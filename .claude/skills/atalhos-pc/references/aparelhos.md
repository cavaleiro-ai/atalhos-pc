# Aparelhos e instalação sem adb

Só o Galaxy Tab A8 (SM-X200, Android 14) foi testado de fato. O restante deste arquivo vem do funcionamento geral do Android e deve ser conferido no aparelho: os nomes dos menus mudam com a marca e a versão. Quando não achar um menu, use a busca do app Configurações.

## Conferir se o aparelho serve

| Item | Como ver | O que fazer |
|---|---|---|
| Sistema | Configurações > Sobre | Android 5.0 ou mais novo serve. iPhone e iPad não funcionam. |
| Tablet ou celular | | O mesmo app. Em tela estreita o layout muda: botões curtos e mesa de som em cima dos pads. Isso só foi conferido simulando a tela no tablet. |
| Arquitetura | | O app é só Java, sem código nativo, então serve para qualquer processador. |
| Tamanho da tela | | A grade se adapta. Em tela pequena, prefira os tamanhos Grande ou Médio. |

## Instalar o APK sem adb

1. Copie `assets\AtalhosPC.apk` para o aparelho, pelo cabo no modo de transferência de arquivos, por nuvem ou como preferir.
2. No aparelho, abra o app de arquivos e toque no APK.
3. O Android pede para autorizar a instalação por aquele app. O usuário autoriza só naquele app, e pode desligar depois.
4. Se aparecer aviso do Play Protect, o usuário decide na tela. Não desative o Play Protect.

## Ligar a depuração USB, se o usuário quiser

Configurações > Sobre > toque 7 vezes em **Número da versão** (em Xiaomi, **Versão do MIUI** ou **Versão do HyperOS**) > volte > **Opções do desenvolvedor** > **Depuração USB**. Ao ligar o cabo, o aparelho pergunta se confia no PC, e quem responde é o usuário.

O adb pode ser baixado oficialmente em `https://dl.google.com/android/repository/platform-tools-latest-windows.zip`. Extraia em `%LOCALAPPDATA%\Android\Sdk\platform-tools`, onde tanto os scripts quanto a opção **USB por adb** do programa o encontram.

Se o Windows não listar o aparelho no adb, pode faltar o driver da marca. Em alguns Samsung, o adb só apareceu com a Ancoragem USB ligada.

## Particularidades por marca

| Marca | Pontos de atenção |
|---|---|
| Samsung | Ancoragem em Conexões > Roteador Wi-Fi e Ancoragem > Ancoragem USB. **Testado:** funciona em tablet só Wi-Fi. |
| Xiaomi, Redmi, POCO | `adb install` costuma exigir **Instalar via USB** nas Opções do desenvolvedor, o que pode pedir conta Mi. Toque simulado pelo adb costuma exigir **Depuração USB (configurações de segurança)**. Se der trabalho, instale manualmente e peça ao usuário para tocar na tela. |
| Motorola, Google Pixel | Ancoragem fica em Rede e internet > Roteador e tethering. |
| Huawei, Honor | Sem serviços do Google em modelos recentes: não afeta o app, que não os usa. |
| Aparelho sem SIM | Algumas marcas escondem ou bloqueiam a Ancoragem USB. Use USB por adb ou Wi-Fi. |
| Operadora | Algumas restringem a ancoragem. Use USB por adb ou Wi-Fi. |

## Cuidados no aparelho

- **Plano de dados:** com a Ancoragem USB, o PC pode passar a navegar pela internet móvel do aparelho.
- **Bateria:** o app não roda em segundo plano. A conexão fecha 30 segundos depois de sair dele e volta ao abrir.
- **Tela:** o app não mantém a tela ligada. Para usar como painel fixo, o usuário ajusta o tempo de tela em Configurações.
- **Permissões:** o app só pede Bluetooth, e só quando esse caminho é escolhido.
