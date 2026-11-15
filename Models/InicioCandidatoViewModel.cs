namespace TalentDesk.Models
{
    // Item da lista "complete seu currículo"
    public record ItemCompletude(string Rotulo, bool Feito);

    public class InicioCandidatoViewModel
    {
        public string PrimeiroNome { get; set; } = string.Empty;
        public bool TemCurriculo { get; set; }

        // ===== Cards de resumo =====
        public int TotalCandidaturas { get; set; }
        public int EmAndamento { get; set; }
        public int Aprovadas { get; set; }
        public int TotalSalvas { get; set; }
        public int VagasNovas { get; set; }
        public const int DiasVagasNovas = 7;

        // ===== Andamento, completude e recomendações =====
        public List<Vaga> Candidaturas { get; set; } = new();
        public List<EventoCandidatura> Atualizacoes { get; set; } = new();
        public List<ItemCompletude> Completude { get; set; } = new();
        public List<Vaga> Recomendadas { get; set; } = new();

        public int PercentualCompleto => Completude.Count == 0
            ? 0
            : (int)Math.Round(Completude.Count(i => i.Feito) * 100.0 / Completude.Count);

        // Etapas mostradas na barra de progresso de cada candidatura
        public static IReadOnlyList<string> Etapas { get; } = new[] { "Inscrito", "Em análise", "Entrevista", "Resultado" };

        // Índice da etapa atual (0 a 3) para o status da candidatura
        public static int EtapaAtual(string? status) => status switch
        {
            StatusCurriculo.EmAnalise => 1,
            StatusCurriculo.Entrevista => 2,
            StatusCurriculo.Aprovado or StatusCurriculo.Reprovado => 3,
            _ => 0
        };
    }
}
