# ADR-0001: Política de Execução Manual da Unity e Governança do Assistente

- **Status:** Aceito
- **Data:** 14/11/2026
- **Decisores:** Desenvolvedor ScaryParty & Antigravity

---

## 1. Contexto
No desenvolvimento com motores como Unity 6, a inicialização do motor via linha de comando (`-batchmode`, `-nographics`, builds e Test Runner) é custosa em tempo de compilação, consumo de memória e pode concorrer com a instância do Unity Editor já aberta pelo desenvolvedor (gerando conflitos de bloqueio na pasta `Library/`).  
Além disso, no uso de agentes de IA, rodar batchmode automaticamente e carregar logs gigantescos no chat consome cotas/tokens sem necessidade.

---

## 2. Decisão
1. Adotar a política documental **`UNITY_EXECUTION=MANUAL_ONLY`**: o agente nunca dispara a Unity em batchmode/headless de forma autônoma.
2. Toda validação de renderização, física, jogabilidade e rede deve ser orientada para o **Unity Editor aberto pelo desenvolvedor**.
3. Adotar regras modulares e condicionais no diretório `.agents/rules/` (`trigger: glob` e `trigger: model_decision`), com um contrato enxuto permanente em `AGENTS.md`.
4. Manter a validação em camadas (L0 a L3), onde L3 (execução CLI) só ocorre com autorização explícita do desenvolvedor para a tarefa corrente.

---

## 3. Consequências

### Positivas
- Elimina o risco de corrupção ou congelamento da `Library/` por instâncias concorrentes da Unity.
- Reduz drasticamente o consumo desnecessário de contexto e tokens com logs de compilação ou batch.
- Proporciona desenvolvimento ágil com o modo TURBO do Antigravity ativo para tarefas de programação, git e documentação.

### Negativas / Cuidados
- Testes automatizados via linha de comando exigem pedido manual deliberado do usuário.
- O desenvolvedor é responsável por alternar para a janela do Unity Editor para testar em PlayMode ou compilar alterações.
