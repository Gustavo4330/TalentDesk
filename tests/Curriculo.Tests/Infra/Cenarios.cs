using System.Net;
using System.Text.RegularExpressions;

namespace Curriculo.Tests.Infra
{
    // Atalhos para montar os personagens dos testes pelo próprio sistema (telas reais)
    public class Cenarios
    {
        private readonly TalentDeskFactory _app;

        public Cenarios(TalentDeskFactory app) => _app = app;

        private static string EmailUnico(string prefixo) => $"{prefixo}.{Guid.NewGuid():N}@talentdesk.test";

        public async Task<Resposta> Entrar(Cliente cliente, string email) =>
            await cliente.Post("/Usuario/Entrar", "/Usuario/Entrar",
                new Dictionary<string, string> { ["Email"] = email, ["Senha"] = TalentDeskFactory.Senha });

        // Candidato logado; com currículo (área/habilidades opcionais) ou sem
        public async Task<(Cliente Cliente, int? CurriculoId, string Email)> Candidato(
            bool comCurriculo = true, string area = "Tecnologia", string cargo = "Desenvolvedor", string habilidades = "")
        {
            var cliente = _app.NovoCliente();
            string email = EmailUnico("candidato");

            var cadastro = await cliente.Post("/Curriculo/Login", "/Usuario/Cadastro", new Dictionary<string, string>
            {
                ["Nome"] = "Candidato Teste",
                ["Email"] = email,
                ["Senha"] = TalentDeskFactory.Senha,
                ["Whatsapp"] = "11999999999",
                ["DataNascimento"] = "1995-05-10"
            });
            Assert.True(cadastro.Redireciona("/Usuario/Entrar"), $"Cadastro falhou: {cadastro.Status}");
            var login = await Entrar(cliente, email);
            Assert.True(login.Redireciona("/Inicio"), $"Login do candidato: {login.Status} -> {login.Destino}");

            if (!comCurriculo) return (cliente, null, email);

            var curriculo = await cliente.Post("/Curriculo/Cadastrar", "/Curriculo/Cadastrar", new Dictionary<string, string>
            {
                ["Nome"] = "Candidato Teste",
                ["Cargo"] = cargo,
                ["Area"] = area,
                ["Idade"] = "28",
                ["AnosExperiencia"] = "3",
                ["Email"] = email,
                ["Cidade"] = "Recife, PE",
                ["Formacao"] = "Sistemas de Informação",
                ["Habilidades"] = habilidades,
                ["Status"] = "Aprovado" // deve ser ignorado: quem define o status é a empresa
            });
            Assert.True(curriculo.Redireciona("/Curriculo/MeuCurriculo"), $"Currículo falhou: {curriculo.Status}");

            var meu = await cliente.Get("/Curriculo/MeuCurriculo");
            return (cliente, meu.IdNoDestino(), email);
        }

        public async Task<Cliente> Superadmin()
        {
            var cliente = _app.NovoCliente();
            // Cria a conta da equipe na primeira vez (o cadastro aberto cria como candidato;
            // o login promove a Superadmin por causa da configuração)
            await cliente.Post("/Curriculo/Login", "/Usuario/Cadastro", new Dictionary<string, string>
            {
                ["Nome"] = "Equipe Talent Desk",
                ["Email"] = TalentDeskFactory.EmailSuperadmin,
                ["Senha"] = TalentDeskFactory.Senha,
                ["Whatsapp"] = "11999999999",
                ["DataNascimento"] = "1990-01-01"
            });
            Assert.True((await Entrar(cliente, TalentDeskFactory.EmailSuperadmin)).Status == HttpStatusCode.Redirect);
            return cliente;
        }

        // Gera o link de convite pela tela do superadmin e devolve o caminho dele
        public async Task<string> Convite(string email, string empresa)
        {
            var superadmin = await Superadmin();
            var tela = await superadmin.Post("/Convites", "/Convites/Criar",
                new Dictionary<string, string> { ["Form.Email"] = email, ["Form.Empresa"] = empresa });

            var link = Regex.Match(tela.Html, "value=\"(http[^\"]*Usuario/Convite\\?token=[^\"]+)\"");
            Assert.True(link.Success, "Link de convite não apareceu na tela");
            return new Uri(WebUtility.HtmlDecode(link.Groups[1].Value)).PathAndQuery;
        }

        // Administrador logado, criado pelo fluxo de convite
        public async Task<(Cliente Cliente, string Email)> Administrador(string empresa)
        {
            string email = EmailUnico("admin");
            string link = await Convite(email, empresa);

            var cliente = _app.NovoCliente();
            var criado = await cliente.Post(link, link, new Dictionary<string, string>
            {
                ["Nome"] = "Admin Teste",
                ["Senha"] = TalentDeskFactory.Senha,
                ["ConfirmarSenha"] = TalentDeskFactory.Senha
            });
            Assert.True(criado.Redireciona("/Usuario/Entrar"), $"Convite falhou: {criado.Status}");
            Assert.True((await Entrar(cliente, email)).Status == HttpStatusCode.Redirect);
            return (cliente, email);
        }

        // Publica uma vaga e devolve o id
        public static async Task<int> PublicarVaga(Cliente empresa, string titulo, string area = "Tecnologia",
                                                   string requisitos = "C#\nSQL")
        {
            var resposta = await empresa.Post("/Vagas/Nova", "/Vagas/Nova", new Dictionary<string, string>
            {
                ["Titulo"] = titulo,
                ["Empresa"] = "Empresa Teste",
                ["Area"] = area,
                ["Cidade"] = "São Paulo, SP",
                ["Modalidade"] = "Híbrido",
                ["TipoContrato"] = "CLT",
                ["Descricao"] = "Vaga criada pelos testes automatizados.",
                ["Requisitos"] = requisitos
            });
            Assert.True(resposta.Redireciona("/Vagas/Detalhes/"), $"Publicar vaga falhou: {resposta.Status}");
            return resposta.IdNoDestino();
        }

        // Id da candidatura de um currículo, lido do quadro da vaga (seletor "mover-{id}")
        public static async Task<int> CandidaturaNoQuadro(Cliente empresa, int vagaId)
        {
            var pagina = await empresa.Get($"/Vagas/Detalhes/{vagaId}");
            var id = Regex.Match(pagina.Html, "id=\"mover-(\\d+)\"");
            Assert.True(id.Success, "Nenhum inscrito no quadro da vaga");
            return int.Parse(id.Groups[1].Value);
        }
    }
}
