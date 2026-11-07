// Versao, nomes e sinais compartilhados pelo programa e pelo instalador.
namespace AtalhosPC
{
    public static class AppInfo
    {
        public const string Version = "2.4";
        public const string Name = "Atalhos PC";
        public const string Publisher = "Cavaleiro AI";

        // Sinais nomeados da sessao do Windows. So uma copia do programa roda por vez.
        public const string SingleMutex = @"Local\AtalhosPC.Unico";
        public const string ShowEvent = @"Local\AtalhosPC.Mostrar";
        // O instalador e o desinstalador usam este para fechar o programa sem matar o processo.
        public const string ExitEvent = @"Local\AtalhosPC.Sair";

        // O instalador abre o programa com este argumento, porque a saudacao ja apareceu no instalador.
        public const string GreetedArg = "--saudacao-vista";
    }
}
