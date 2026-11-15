using TalentDesk.Models;
using TalentDesk.Repositories;

namespace TalentDesk.Services
{
    // Monta a tela inicial do candidato: resumo, andamento das candidaturas,
    // currículo completo e vagas recomendadas
    public class InicioCandidatoService
    {
        private readonly VagaRepository _vagaRepository;
        private readonly CurriculoRepository _curriculoRepository;

        public InicioCandidatoService(VagaRepository vagaRepository, CurriculoRepository curriculoRepository)
        {
            _vagaRepository = vagaRepository;
            _curriculoRepository = curriculoRepository;
        }

        public InicioCandidatoViewModel Montar(int usuarioId, string primeiroNome)
        {
            int? curriculoId = _curriculoRepository.ObterIdPorUsuario(usuarioId);
            var curriculo = curriculoId.HasValue ? _curriculoRepository.ObterPorId(curriculoId.Value) : null;

            var viewModel = new InicioCandidatoViewModel
            {
                PrimeiroNome = primeiroNome,
                TemCurriculo = curriculo != null,
                TotalSalvas = _vagaRepository.ContarSalvas(usuarioId),
                VagasNovas = _vagaRepository.ContarAbertasRecentes(InicioCandidatoViewModel.DiasVagasNovas)
            };

            if (curriculo == null)
            {
                return viewModel;
            }

            viewModel.Candidaturas = _vagaRepository.ListarCandidaturas(curriculo.Id, usuarioId);
            viewModel.TotalCandidaturas = viewModel.Candidaturas.Count;
            viewModel.EmAndamento = viewModel.Candidaturas.Count(v => v.StatusCandidatura is string status && StatusCurriculo.EmProcesso.Contains(status));
            viewModel.Aprovadas = viewModel.Candidaturas.Count(v => v.StatusCandidatura == StatusCurriculo.Aprovado);
            viewModel.Atualizacoes = _vagaRepository.ListarHistorico(curriculo.Id, limite: 8);
            viewModel.Completude = AvaliarCompletude(curriculo);
            viewModel.Recomendadas = Recomendar(curriculo, usuarioId);

            return viewModel;
        }

        // O que falta para o currículo ficar completo (dicas na tela inicial)
        public static List<ItemCompletude> AvaliarCompletude(Curriculo curriculo) => new()
        {
            new("Dados básicos", true),
            new("Resumo profissional", !string.IsNullOrWhiteSpace(curriculo.Resumo)),
            new("Telefone para contato", !string.IsNullOrWhiteSpace(curriculo.Telefone)),
            new("Perfil do LinkedIn", !string.IsNullOrWhiteSpace(curriculo.Linkedin)),
            new("Pelo menos 3 competências", curriculo.Habilidades.Count >= 3),
            new("Pelo menos uma experiência", curriculo.Experiencias.Count > 0),
            new("Instituição de ensino", !string.IsNullOrWhiteSpace(curriculo.Instituicao))
        };

        // Vagas abertas mais parecidas com o currículo (área, cargo e competências),
        // sem as que o candidato já se inscreveu
        private List<Vaga> Recomendar(Curriculo curriculo, int usuarioId, int limite = 3)
        {
            var habilidades = curriculo.Habilidades
                .Select(h => Conexao.SemAcento(h.Trim()) ?? string.Empty)
                .Where(h => h.Length >= 2)
                .Distinct()
                .ToList();
            string? cargo = Conexao.SemAcento(curriculo.Cargo.Trim());

            return _vagaRepository.ListarAbertas(new FiltroVaga(), curriculo.Id, usuarioId)
                .Where(vaga => !vaga.JaCandidatado)
                .Select(vaga =>
                {
                    string texto = Conexao.SemAcento($"{vaga.Titulo} {vaga.Requisitos} {vaga.Descricao}") ?? string.Empty;
                    int pontos = 0;
                    if (string.Equals(vaga.Area, curriculo.Area, StringComparison.OrdinalIgnoreCase)) pontos += 3;
                    if (!string.IsNullOrEmpty(cargo) && texto.Contains(cargo)) pontos += 2;
                    pontos += habilidades.Count(h => texto.Contains(h));
                    return (vaga, pontos);
                })
                .Where(item => item.pontos > 0)
                .OrderByDescending(item => item.pontos)
                .ThenByDescending(item => item.vaga.CriadoEm)
                .Take(limite)
                .Select(item => item.vaga)
                .ToList();
        }
    }
}
