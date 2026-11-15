using System.ComponentModel.DataAnnotations;

namespace TalentDesk.Models
{
    public static class StatusVaga
    {
        public const string Aberta = "Aberta";
        public const string Encerrada = "Encerrada";
    }

    // Opções fixas dos selects do formulário de vaga
    public static class OpcoesVaga
    {
        public static IReadOnlyList<string> Modalidades { get; } = new[] { "Presencial", "Híbrido", "Remoto" };

        public static IReadOnlyList<string> TiposContrato { get; } = new[] { "CLT", "PJ", "Estágio", "Temporário", "Freelancer" };
    }

    public class Vaga
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Empresa { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Modalidade { get; set; } = string.Empty;
        public string TipoContrato { get; set; } = string.Empty;
        public string? FaixaSalarial { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string? Requisitos { get; set; }
        public string Status { get; set; } = StatusVaga.Aberta;
        public int CriadoPor { get; set; }
        public int? EmpresaId { get; set; }
        public DateTime CriadoEm { get; set; }

        // Preenchidos nas consultas
        public int TotalInscritos { get; set; }

        // Visão do candidato logado: status da candidatura dele (null = não se inscreveu)
        public string? StatusCandidatura { get; set; }
        public DateTime? CandidaturaAtualizadaEm { get; set; }
        public bool Salva { get; set; }

        public bool JaCandidatado => StatusCandidatura != null;

        public bool Aberta => Status == StatusVaga.Aberta;
        public string PublicadaHa => Curriculo.TempoDesde(CriadoEm, "Publicada");

        // Requisitos digitados um por linha viram lista
        public IEnumerable<string> ListaRequisitos =>
            (Requisitos ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(linha => linha.TrimStart('-', '•', '*', ' '))
                .Where(linha => linha.Length > 0);
    }

    public class VagaFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }
        public bool EhEdicao => Id.HasValue;

        [Required(ErrorMessage = "Informe o título da vaga.")]
        [StringLength(120, ErrorMessage = "Use até 120 caracteres.")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o nome da empresa.")]
        [StringLength(120, ErrorMessage = "Use até 120 caracteres.")]
        public string Empresa { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a área.")]
        [StringLength(80, ErrorMessage = "Use até 80 caracteres.")]
        public string Area { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a cidade.")]
        [StringLength(80, ErrorMessage = "Use até 80 caracteres.")]
        public string Cidade { get; set; } = string.Empty;

        [Required(ErrorMessage = "Escolha a modalidade.")]
        public string Modalidade { get; set; } = OpcoesVaga.Modalidades[0];

        [Required(ErrorMessage = "Escolha o tipo de contrato.")]
        [Display(Name = "Tipo de contrato")]
        public string TipoContrato { get; set; } = OpcoesVaga.TiposContrato[0];

        [StringLength(60, ErrorMessage = "Use até 60 caracteres.")]
        [Display(Name = "Faixa salarial")]
        public string? FaixaSalarial { get; set; }

        [Required(ErrorMessage = "Descreva a vaga.")]
        [StringLength(4000, ErrorMessage = "Use até 4000 caracteres.")]
        public string Descricao { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Use até 2000 caracteres.")]
        public string? Requisitos { get; set; }

        public List<string> SugestoesAreas { get; set; } = new();
        public List<string> SugestoesCidades { get; set; } = new();

        // Os selects só aceitam as opções oferecidas
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!OpcoesVaga.Modalidades.Contains(Modalidade))
                yield return new ValidationResult("Modalidade inválida.", new[] { nameof(Modalidade) });
            if (!OpcoesVaga.TiposContrato.Contains(TipoContrato))
                yield return new ValidationResult("Tipo de contrato inválido.", new[] { nameof(TipoContrato) });
        }

        public static VagaFormViewModel DeVaga(Vaga vaga) => new()
        {
            Id = vaga.Id,
            Titulo = vaga.Titulo,
            Empresa = vaga.Empresa,
            Area = vaga.Area,
            Cidade = vaga.Cidade,
            Modalidade = vaga.Modalidade,
            TipoContrato = vaga.TipoContrato,
            FaixaSalarial = vaga.FaixaSalarial,
            Descricao = vaga.Descricao,
            Requisitos = vaga.Requisitos
        };

        public Vaga ParaVaga() => new()
        {
            Titulo = Titulo.Trim(),
            Empresa = Empresa.Trim(),
            Area = Area.Trim(),
            Cidade = Cidade.Trim(),
            Modalidade = Modalidade,
            TipoContrato = TipoContrato,
            FaixaSalarial = string.IsNullOrWhiteSpace(FaixaSalarial) ? null : FaixaSalarial.Trim(),
            Descricao = Descricao.Trim(),
            Requisitos = string.IsNullOrWhiteSpace(Requisitos) ? null : Requisitos.Trim()
        };
    }

    // Filtros da lista de vagas do candidato (ex.: ?busca=dev&modalidade=Remoto)
    public class FiltroVaga
    {
        public const string AbaAbertas = "abertas";
        public const string AbaCandidaturas = "candidaturas";
        public const string AbaSalvas = "salvas";

        public string? Busca { get; set; }
        public string? Area { get; set; }
        public string? Modalidade { get; set; }
        public string Aba { get; set; } = AbaAbertas;

        public bool EhCandidaturas => Aba == AbaCandidaturas;
        public bool EhSalvas => Aba == AbaSalvas;
        public bool TemFiltro => !string.IsNullOrWhiteSpace(Busca) || !string.IsNullOrWhiteSpace(Area) || !string.IsNullOrWhiteSpace(Modalidade);

        public void Normalizar()
        {
            Busca = string.IsNullOrWhiteSpace(Busca) ? null : Busca.Trim()[..Math.Min(Busca.Trim().Length, 80)];
            Area = string.IsNullOrWhiteSpace(Area) ? null : Area.Trim();
            Modalidade = OpcoesVaga.Modalidades.Contains(Modalidade ?? "") ? Modalidade : null;
            Aba = Aba is AbaCandidaturas or AbaSalvas ? Aba : AbaAbertas;
        }
    }

    public class VagasIndexViewModel
    {
        public bool EhEmpresa { get; set; }
        public FiltroVaga Filtro { get; set; } = new();
        public List<Vaga> Vagas { get; set; } = new();
        public List<string> Areas { get; set; } = new();

        // Candidato sem currículo não consegue se candidatar
        public bool TemCurriculo { get; set; }
        public int TotalCandidaturas { get; set; }
        public int TotalSalvas { get; set; }
    }

    public class VagaDetalhesViewModel
    {
        public Vaga Vaga { get; set; } = new();
        public bool EhDono { get; set; }
        public bool TemCurriculo { get; set; }
        public List<Inscrito> Inscritos { get; set; } = new();

        // Quadro da empresa: uma coluna por etapa, na ordem do processo
        public IEnumerable<(string Status, List<Inscrito> Inscritos)> Quadro =>
            StatusCurriculo.Todos.Select(status => (status, Inscritos.Where(i => i.Status == status).ToList()));
    }

    // Candidato inscrito numa vaga, com o status da candidatura
    public class Inscrito
    {
        public int CandidaturaId { get; set; }
        public int CurriculoId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public int AnosExperiencia { get; set; }
        public string Status { get; set; } = StatusCurriculo.Novo;
        public DateTime InscritoEm { get; set; }

        public string ExperienciaTexto => AnosExperiencia switch
        {
            <= 0 => "Menos de 1 ano",
            1 => "1 ano",
            _ => $"{AnosExperiencia} anos"
        };
    }

    // Um passo da linha do tempo de uma candidatura
    public class EventoCandidatura
    {
        public int VagaId { get; set; }
        public string VagaTitulo { get; set; } = string.Empty;
        public string Empresa { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Data { get; set; }

        public string Descricao => Status == StatusCurriculo.Novo
            ? "Candidatura enviada"
            : $"Status alterado para {Status}";
    }
}
