document.addEventListener('DOMContentLoaded', () => {
    const rows = Array.from(document.querySelectorAll('tbody tr[data-id]'));
    const searchName = document.getElementById('searchName');
    const globalSearch = document.getElementById('globalSearch');
    const filterArea = document.getElementById('filterArea');
    const filterFormacao = document.getElementById('filterFormacao');
    const filterIdade = document.getElementById('filterIdade');
    const filterButton = document.querySelector('.filter-button');
    const closePanelButton = document.getElementById('closePanel');
    const detailsPanel = document.getElementById('detailsPanel');
    const allCandidates = Array.isArray(window.curriculos) ? window.curriculos : [];

    const showCandidate = (candidate) => {
        if (!candidate || !detailsPanel) return;

        document.getElementById('detailName').textContent = candidate.Nome || 'Currículo';
        document.getElementById('detailImage').src = candidate.FotoUrl || 'https://i.pravatar.cc/150';
        document.getElementById('detailEmail').textContent = candidate.Email || 'Sem e-mail';
        document.getElementById('detailPhone').textContent = candidate.Telefone || 'Sem telefone';
        document.getElementById('detailCity').textContent = candidate.Cidade || 'Sem cidade';
        document.getElementById('detailLinkedin').textContent = candidate.Linkedin || 'Sem LinkedIn';
        document.getElementById('detailResumo').textContent = candidate.Resumo || '';

        const experienciaContainer = document.getElementById('detailExperiencia');
        experienciaContainer.innerHTML = '';
        if (candidate.Experiencias && candidate.Experiencias.length) {
            candidate.Experiencias.forEach((experiencia) => {
                const item = document.createElement('div');
                item.className = 'timeline-item';
                item.innerHTML = `
                    <div class="timeline-dot"></div>
                    <div class="timeline-content">
                        <h5>${experiencia.Cargo || 'Cargo'} – ${experiencia.Empresa || 'Empresa'}</h5>
                        <span>${experiencia.Periodo || ''}</span>
                        <p>${experiencia.Descricao || ''}</p>
                    </div>
                `;
                experienciaContainer.appendChild(item);
            });
        }

        document.getElementById('detailFormacao').textContent = candidate.FormacaoDetalhada || candidate.Formacao || 'Formação não informada';

        const skillsContainer = document.getElementById('detailSkills');
        skillsContainer.innerHTML = '';
        if (candidate.Habilidades && candidate.Habilidades.length) {
            candidate.Habilidades.forEach((skill) => {
                const tag = document.createElement('span');
                tag.className = 'skill-tag';
                tag.textContent = skill;
                skillsContainer.appendChild(tag);
            });
        }

        detailsPanel.classList.add('open');
    };

    const filterRows = () => {
        const nameValue = (searchName?.value || '').trim().toLowerCase();
        const globalValue = (globalSearch?.value || '').trim().toLowerCase();
        const areaValue = filterArea?.value || '';
        const formacaoValue = filterFormacao?.value || '';
        const idadeValue = filterIdade?.value || '';

        rows.forEach((row) => {
            const candidateName = (row.dataset.name || '').toLowerCase();
            const candidateArea = row.dataset.area || '';
            const candidateFormacao = row.dataset.formacao || '';
            const candidateIdade = row.dataset.idade || '';
            const matchesName = !nameValue && !globalValue ? true : candidateName.includes(nameValue || globalValue);
            const matchesArea = !areaValue || candidateArea === areaValue;
            const matchesFormacao = !formacaoValue || candidateFormacao === formacaoValue;
            const matchesIdade = !idadeValue || candidateIdade === idadeValue;

            row.style.display = matchesName && matchesArea && matchesFormacao && matchesIdade ? '' : 'none';
        });
    };

    searchName?.addEventListener('input', filterRows);
    globalSearch?.addEventListener('input', filterRows);
    filterArea?.addEventListener('change', filterRows);
    filterFormacao?.addEventListener('change', filterRows);
    filterIdade?.addEventListener('change', filterRows);
    filterButton?.addEventListener('click', filterRows);

    document.querySelectorAll('.view-btn').forEach((button) => {
        button.addEventListener('click', () => {
            const id = Number(button.dataset.id);
            const candidate = allCandidates.find((item) => Number(item.Id) === id);
            if (candidate) showCandidate(candidate);
        });
    });

    closePanelButton?.addEventListener('click', () => {
        detailsPanel?.classList.remove('open');
    });

    const firstCandidate = allCandidates[0];
    if (firstCandidate) showCandidate(firstCandidate);
});
