using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Curriculo.Tests.Infra
{
    // Sobe o Talent Desk inteiro em memória, com um banco SQLite temporário só dos testes.
    // Uma instância é compartilhada por todos os testes (o caminho do banco é estático no app).
    public class TalentDeskFactory : WebApplicationFactory<Program>
    {
        public const string EmailSuperadmin = "equipe@talentdesk.test";
        public const string Senha = "Senha-de-teste-123";

        private readonly string _pasta = Path.Combine(Path.GetTempPath(), "talentdesk-testes-" + Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // "Testing": não carrega appsettings.Development nem os user-secrets da máquina
            builder.UseEnvironment("Testing");
            builder.UseSetting("Banco:Caminho", Path.Combine(_pasta, "testes.db"));
            builder.UseSetting("Superadmin:Emails:0", EmailSuperadmin);
        }

        public Cliente NovoCliente() => new(CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        }));

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { Directory.Delete(_pasta, recursive: true); } catch (IOException) { /* arquivo ainda aberto: o SO limpa a pasta temporária */ }
        }
    }

    [CollectionDefinition(Nome)]
    public class ColecaoTalentDesk : ICollectionFixture<TalentDeskFactory>
    {
        public const string Nome = "TalentDesk";
    }

    public record Resposta(HttpStatusCode Status, string? Destino, string Html)
    {
        public bool Redireciona(string trecho) =>
            Status == HttpStatusCode.Redirect && Destino != null && Destino.Contains(trecho, StringComparison.OrdinalIgnoreCase);

        // Último número do endereço de destino (ex.: /Vagas/Detalhes/7 -> 7)
        public int IdNoDestino() => int.Parse(Regex.Match(Destino ?? "", @"(\d+)(?:#.*)?$").Groups[1].Value);
    }

    // Um "navegador" com cookies próprios (sessão e antifalsificação)
    public class Cliente
    {
        private static readonly Regex Token = new(
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.Compiled);

        private readonly HttpClient _http;

        public Cliente(HttpClient http) => _http = http;

        public async Task<Resposta> Get(string caminho)
        {
            using var resposta = await _http.GetAsync(caminho);
            return await Ler(resposta);
        }

        // Abre a página do formulário, pega o token antifalsificação e envia os dados
        public async Task<Resposta> Post(string paginaDoFormulario, string acao, IDictionary<string, string>? dados = null)
        {
            var pagina = await Get(paginaDoFormulario);
            var token = Token.Match(pagina.Html);
            Assert.True(token.Success, $"Sem token antifalsificação em {paginaDoFormulario} ({pagina.Status} -> {pagina.Destino})");

            var campos = new Dictionary<string, string>(dados ?? new Dictionary<string, string>())
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
            };
            using var resposta = await _http.PostAsync(acao, new FormUrlEncodedContent(campos));
            return await Ler(resposta);
        }

        private static async Task<Resposta> Ler(HttpResponseMessage resposta) =>
            new(resposta.StatusCode, resposta.Headers.Location?.OriginalString, await resposta.Content.ReadAsStringAsync());
    }
}
