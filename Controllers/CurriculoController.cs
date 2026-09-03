using Microsoft.AspNetCore.Mvc;
using TalentDesk.Models;
using TalentDesk;
using TalentDesk.Repositories;
using CurriculoModel = TalentDesk.Models.Curriculo;

namespace TalentDesk.Controllers
{
    public class CurriculoController : Controller
    {
        private readonly UsuarioRepository _usuarioRepository;

        public CurriculoController()
        {
            _usuarioRepository = new UsuarioRepository();
        }

        private static readonly List<CurriculoModel> curriculos = new()
        {
            new CurriculoModel
            {
                Id = 1,
                Nome = "Amanda Bonfim",
                Area = "Recursos Humanos",
                Formacao = "Psicologia",
                FormacaoDetalhada = "Psicologia – Bacharelado",
                Idade = 28,
                FotoUrl = "https://i.pravatar.cc/150?img=32",
                Email = "amanda.bonfim@email.com",
                Telefone = "(11) 98765-4321",
                Cidade = "São Paulo, SP",
                Linkedin = "linkedin.com/in/amandabonfim",
                Resumo = "Profissional de Recursos Humanos com experiência em recrutamento e seleção, desenvolvimento organizacional e gestão de pessoas. Apaixonada por conectar talentos e impulsionar resultados através de pessoas.",
                Experiencias = new List<ExperienciaProfissional>
                {
                    new() { Cargo = "Analista de RH Pleno", Empresa = "Empresa X", Periodo = "Jan/2022 – Atual", Descricao = "Atuação em recrutamento e seleção, treinamento e desenvolvimento, onboarding e gestão de benefícios." },
                    new() { Cargo = "Analista de RH Júnior", Empresa = "Empresa Y", Periodo = "Jun/2019 – Dez/2021", Descricao = "Apoio nos processos de R&S, integração de colaboradores e ações de endomarketing." }
                },
                Habilidades = new List<string> { "Recrutamento e Seleção", "Treinamento e Desenvolvimento", "Comunicação", "Liderança", "Pacote Office", "Excel" }
            },
            new CurriculoModel
            {
                Id = 2,
                Nome = "Felipe Castro",
                Area = "Desenvolvimento de Software",
                Formacao = "Ciência da Computação",
                FormacaoDetalhada = "Ciência da Computação – Bacharelado",
                Idade = 25,
                FotoUrl = "https://i.pravatar.cc/150?img=12",
                Email = "felipe.castro@email.com",
                Telefone = "(21) 99876-1122",
                Cidade = "Rio de Janeiro, RJ",
                Linkedin = "linkedin.com/in/felipecastro",
                Resumo = "Desenvolvedor com foco em aplicações web, arquitetura de software e entrega de soluções escaláveis para negócios digitais.",
                Experiencias = new List<ExperienciaProfissional>
                {
                    new() { Cargo = "Desenvolvedor Front-end", Empresa = "Empresa A", Periodo = "Mai/2021 – Atual", Descricao = "Criação de interfaces modernas e experiências de usuário de alta performance." },
                    new() { Cargo = "Estagiário de TI", Empresa = "Empresa B", Periodo = "Jan/2019 – Abr/2021", Descricao = "Manutenção de sistemas, testes e suporte técnico em aplicações web." }
                },
                Habilidades = new List<string> { "React", "C#", "JavaScript", "HTML", "CSS", "SQL" }
            },
            new CurriculoModel
            {
                Id = 3,
                Nome = "Juliana Gomes",
                Area = "Marketing",
                Formacao = "Publicidade e Propaganda",
                FormacaoDetalhada = "Publicidade e Propaganda – Bacharelado",
                Idade = 27,
                FotoUrl = "https://i.pravatar.cc/150?img=15",
                Email = "juliana.gomes@email.com",
                Telefone = "(31) 99123-4567",
                Cidade = "Belo Horizonte, MG",
                Linkedin = "linkedin.com/in/julianagomes",
                Resumo = "Especialista em campanhas digitais, branding e comunicação estratégica com foco em geração de demanda e crescimento de marcas.",
                Experiencias = new List<ExperienciaProfissional>
                {
                    new() { Cargo = "Coordenadora de Marketing", Empresa = "Empresa Z", Periodo = "Ago/2021 – Atual", Descricao = "Planejamento de campanhas e liderança de estratégias de comunicação digital." },
                    new() { Cargo = "Analista de Marketing", Empresa = "Empresa W", Periodo = "Jan/2019 – Jul/2021", Descricao = "Gestão de tráfego pago, social media e conteúdo institucional." }
                },
                Habilidades = new List<string> { "Marketing Digital", "Branding", "SEO", "Google Ads", "Tráfego Pago", "Social Media" }
            },
            new CurriculoModel
            {
                Id = 4,
                Nome = "Rafael Silva",
                Area = "Análise de Dados",
                Formacao = "Estatística",
                FormacaoDetalhada = "Estatística – Bacharelado",
                Idade = 26,
                FotoUrl = "https://i.pravatar.cc/150?img=18",
                Email = "rafael.silva@email.com",
                Telefone = "(41) 98877-6655",
                Cidade = "Curitiba, PR",
                Linkedin = "linkedin.com/in/rafaelsilva",
                Resumo = "Analista de dados com experiência em modelagem, visualização de indicadores e apoio à tomada de decisão com dados de negócio.",
                Experiencias = new List<ExperienciaProfissional>
                {
                    new() { Cargo = "Analista de Dados", Empresa = "Empresa K", Periodo = "Fev/2022 – Atual", Descricao = "Construção de dashboards, relatórios e automações para inteligência de negócio." },
                    new() { Cargo = "BI Analyst", Empresa = "Empresa M", Periodo = "Jul/2019 – Jan/2022", Descricao = "Tratamento de dados, geração de KPIs e suporte à área comercial." }
                },
                Habilidades = new List<string> { "Power BI", "SQL", "Python", "Excel Avançado", "Dashboard", "Business Intelligence" }
            },
            new CurriculoModel
            {
                Id = 5,
                Nome = "Larissa Albuquerque",
                Area = "Design",
                Formacao = "Design Gráfico",
                FormacaoDetalhada = "Design Gráfico – Tecnólogo",
                Idade = 24,
                FotoUrl = "https://i.pravatar.cc/150?img=20",
                Email = "larissa.albuquerque@email.com",
                Telefone = "(85) 98745-1123",
                Cidade = "Fortaleza, CE",
                Linkedin = "linkedin.com/in/larissaalbuquerque",
                Resumo = "Designer visual com foco em identidade visual, UI/UX e materiais de comunicação para marcas e produtos digitais.",
                Experiencias = new List<ExperienciaProfissional>
                {
                    new() { Cargo = "Designer Pleno", Empresa = "Empresa T", Periodo = "Ago/2021 – Atual", Descricao = "Criação de sistemas visuais e desenvolvimento de interfaces responsivas." },
                    new() { Cargo = "Designer Jr.", Empresa = "Empresa P", Periodo = "Jan/2019 – Jul/2021", Descricao = "Produção de peças digitais e apoio em campanhas de comunicação." }
                },
                Habilidades = new List<string> { "UI/UX", "Adobe Creative Suite", "Figma", "Branding", "Illustrator", "Photoshop" }
            },
            new CurriculoModel
            {
                Id = 6,
                Nome = "Victor Pereira",
                Area = "Administração",
                Formacao = "Administração",
                FormacaoDetalhada = "Administração – Bacharelado",
                Idade = 29,
                FotoUrl = "https://i.pravatar.cc/150?img=27",
                Email = "victor.pereira@email.com",
                Telefone = "(11) 99568-3468",
                Cidade = "São Paulo, SP",
                Linkedin = "linkedin.com/in/victorpereira",
                Resumo = "Gestor administrativo com experiência em processos internos, organização operacional e suporte à liderança em ambientes dinâmicos.",
                Experiencias = new List<ExperienciaProfissional>
                {
                    new() { Cargo = "Analista Administrativo", Empresa = "Empresa L", Periodo = "Mar/2021 – Atual", Descricao = "Acompanhamento de rotinas administrativas, contratos e otimização de processos internos." },
                    new() { Cargo = "Assistente Administrativo", Empresa = "Empresa S", Periodo = "Jan/2018 – Fev/2021", Descricao = "Atendimento, organização documental e apoio na gestão de operações corporativas." }
                },
                Habilidades = new List<string> { "Gestão Administrativa", "Organização", "Atendimento", "Processos", "Conciliação", "Excel" }
            }
        };

        public IActionResult Index()
        {
            return View(curriculos);
        }

        [HttpGet]
        public IActionResult TestDb()
        {
            try
            {
                using var conexao = Conexao.CriarConexao();
                conexao.Open();
                return Content("Conectado ao banco com sucesso!");
            }
            catch (Exception ex)
            {
                return Content("Erro ao conectar:\n" + ex.Message);
            }
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var usuario = _usuarioRepository.ObterPorEmail(model.Email);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(model.Senha, usuario.Senha))
            {
                ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
                return View(model);
            }

            TempData["MensagemSucesso"] = $"Bem-vindo(a), {usuario.Nome}! Seu acesso foi validado.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Detalhes(int id)
        {
            var curriculo = curriculos.FirstOrDefault(x => x.Id == id);

            if (curriculo == null)
            {
                return NotFound();
            }

            return View(curriculo);
        }
    }
}