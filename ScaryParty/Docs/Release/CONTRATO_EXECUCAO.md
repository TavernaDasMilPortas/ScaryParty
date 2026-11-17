# Contrato de execução das fases

## Caminhos

Este pacote fica em `Docs/Release/` dentro da raiz Unity — a pasta que contém `Assets`, `Packages` e `ProjectSettings`. Neste repositório ela normalmente é `ScaryParty/`, portanto o caminho a partir da raiz Git será `ScaryParty/Docs/Release/`. Descubra a raiz antes de editar; não crie duas cópias. Os caminhos abaixo são relativos à raiz Unity.

## Trabalho autorizado

O pedido de executar uma fase autoriza inspeção, implementação, ajustes de configuração necessários e verificações disponíveis daquela fase. Não pare só depois de escrever um plano. Faça escolhas técnicas reversíveis sem pedir aprovação a cada arquivo. Preserve escopo e decisões do ROADMAP. Não avance automaticamente à fase seguinte.

Leia AGENTS aplicáveis, `Docs/INDEX.md`, trechos pertinentes de `Docs/CURRENT_STATE.md`, este contrato, o prompt ativo e os relatórios anteriores. Leia o roadmap inicialmente para conhecer regras globais e depois apenas as seções pertinentes. Inspecione código por caminhos/símbolos; não releia todo o repositório a cada passo.

Antes de editar, registre branch, SHA e `git status --short`. O commit do roadmap é referência histórica, não ordem para fazer checkout. Trabalhe sobre a versão local atual, reconciliando o plano com as alterações existentes. Não descarte trabalho do usuário, não force push, não faça merge/deploy. Não faça commit/push sem instrução específica. Preserve `.meta`, GUIDs, referências serializadas e overrides. Não atualize pacotes ou refatore sistemas não relacionados por conveniência.

Reaproveite domínio, DTOs, serviços, wizards, UI Toolkit e feedback existentes. Toda ação compartilhada precisa de validação autoritativa, replicação e limpeza de estado. Teste host e cliente; não considere solução exclusiva do host como concluída.

## Validação e autonomia

Respeite `UNITY_EXECUTION=MANUAL_ONLY`: não iniciar Unity/Hub, batchmode, builds, servidores ou loops headless. Use o Editor já aberto por integração disponível. Sem acesso, continue tudo que não depende dele e registre instruções manuais precisas. Não alegue compilação, execução ou aprovação de testes que não aconteceram. Não substitua testes de comportamento por buscas textuais.

Execute verificações estáticas pertinentes e testes existentes afetados. Acrescente testes de regressão significativos para regras alteradas, sem inflar a suíte com testes triviais de implementação. Repita depois de nova alteração/falha relevante; não faça loops de build para obter segurança aparente.

Falta de teste manual não impede concluir trabalho independente, mas impede declarar validação final. Se uma falha externa bloquear parte da fase, registre causa/evidência e termine as outras tarefas possíveis. Pergunte apenas por decisão realmente necessária, perda de dados ou bloqueio sem alternativa autorizada.

## Checkpoints e encerramento

Mantenha `STATUS.md` e `Reports/Mx_Relatorio.md` após cada bloco importante. Registre arquivos alterados, achados, testes, pendências e próxima ação exata. Não reescreva resultados anteriores como se fossem desta execução.

Estados: NÃO INICIADA; EM EXECUÇÃO; IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE; VALIDADA; BLOQUEADA. Nunca marque VALIDADA com testes críticos pendentes. Uma fase posterior pode ter tarefas independentes executáveis, mas só avance se o usuário pedir; carry-overs críticos não somem do relatório.

Ao terminar, atualize `Docs/CURRENT_STATE.md`, `Docs/CHANGELOG.md`, matriz de testes e relatório da fase. Não substitua documentação existente por resumos que removam informação útil. Entregue: o que mudou, arquivos, verificações executadas, riscos, passos manuais e próximo bloqueio. Diferencie implementação concluída de aceite jogável.
