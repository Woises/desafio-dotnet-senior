# Desafio Técnico — Engenheiro(a) de Software Sênior (.NET / Oracle)

## Contexto

O **Nota Carioca** é a plataforma de emissão de notas fiscais de serviço eletrônicas (NFS-e) do Rio de Janeiro: um monolito .NET Framework com Oracle 19c que processa milhões de notas por ano. Cada nota emitida vira ISS a recolher; cada guia errada vira reclamação na ouvidoria, retrabalho na fiscalização e, no limite, cobrança indevida a um contribuinte. Prazos regulatórios não esperam.

Este repositório contém uma **réplica didática** de um pedaço desse sistema — com a cara que sistemas de 10+ anos têm: C# no estilo .NET Framework, um projeto em VB.NET, regra de negócio dentro de packages e triggers PL/SQL, e web services SOAP usados por milhares de ERPs de contribuintes.

Você acabou de entrar no time de sustentação. É segunda-feira, e a fila de chamados abaixo é sua.

## O sistema

```
legado/
  src/NotaCarioca.Dominio/      VB.NET — cálculo de ISS, validação de CPF/CNPJ, competência
  src/NotaCarioca.Dados/        C# — acesso a dados (ADO.NET / ODP.NET)
  src/NotaCarioca.Servicos/     C# — processamento de lotes de RPS, consultas, cancelamento, XML
  src/NotaCarioca.WebServices/  host que emula o endpoint ASMX (/NfseWS.asmx)
  tests/NotaCarioca.Testes/     os poucos testes que existem hoje
db/                             schema, packages, triggers e cargas (Oracle)
tools/                          gerador de lotes de RPS e exemplos de requisições SOAP
```

Para subir tudo:

```bash
docker compose up -d --build     # Oracle + web services
# primeira subida cria o schema e carrega ~2 milhões de notas: leva alguns minutos
curl http://localhost:8080/health
just lote 1000                   # gera e envia um lote com 1.000 RPS
```

Requisições de exemplo para cada operação estão em `tools/exemplos/`. Comandos úteis estão no `justfile`.

Sobre a stack:

- Os projetos do legado compilam em **`net48` e `net8.0`** com **C# 7.3**. Em produção o código roda em .NET Framework 4.8 sobre IIS. O host em `NotaCarioca.WebServices` só existe para você rodar a réplica em qualquer sistema operacional. Suas mudanças no legado precisam continuar compilando em `net48`, ou seja, sem C# moderno.
- O banco local é Oracle 23ai Free, mas **produção é 19c**: não use recursos exclusivos do 23ai.
- Em produção a tabela `NFSE` tem centenas de milhões de linhas e o sistema não para: alteração de schema que trava a tabela por minutos não é aceitável.

## Regras de negócio

- Base de cálculo = valor dos serviços − deduções. A alíquota vem do item da lista de serviços. ISS = base × alíquota, arredondado em 2 casas.
- MEI não recolhe ISS pela nota (ISS = 0).
- ISS retido é recolhido pelo tomador e **não** entra na guia do prestador.
- A guia (DAM) de um prestador numa competência é a soma do ISS das notas normais (não canceladas, não substituídas) e não retidas daquela competência. Vencimento: dia 10 do mês seguinte.
- **NFS-e emitida é documento fiscal**: seus valores nunca mudam depois da emissão.
- **A numeração da NFS-e é sequencial por prestador, sem lacunas.**
- Um RPS (prestador + série + número) só pode virar **uma** NFS-e. Reenviar o mesmo RPS não pode gerar outra nota (erro `E060` para aquele RPS).
- O contribuinte pode cancelar uma nota até o dia 10 (inclusive) do mês seguinte à competência. Não pode cancelar se a guia da competência já foi paga, nem cancelar uma nota substituída.

## A fila de chamados

Os chamados chegam como chegariam de verdade: pelo atendimento, pela fiscalização, pelo DBA. Ninguém sabe a causa, e o que os chamados afirmam pode estar incompleto.

### CH-01 — "A guia não bate com as notas"

> Contribuinte abriu reclamação na ouvidoria: somou o ISS de todas as notas de setembro/2026 e o total não confere com a guia; a diferença é de alguns centavos. A fiscalização diz que não é o primeiro caso e quer três respostas: o erro está nas notas ou na guia? Desde quando acontece? Quantos contribuintes foram afetados, e em quanto?
>
> Caso reportado: prestador de inscrição municipal `10000001`, competência 09/2026.

### CH-02 — "ORA-00001 e nota em duplicidade no fechamento do mês"

> Nos últimos dias do mês, quando os grandes emissores mandam lotes grandes, aparece `ORA-00001` no log do processamento de lotes. Um contribuinte reclamou que um mesmo RPS gerou duas NFS-e e outro diz que o retorno do lote "veio faltando RPS". Com lote pequeno, ninguém conseguiu reproduzir.

### CH-03 — "Lote grande derruba o processamento"

> Uma rede hospitalar envia lotes de 50 mil RPS. Quando um desses entra, o consumo de memória do processo dispara, o pool do IIS recicla e tudo que estava em processamento recomeça. O padrão de integração permite lotes desse tamanho: recusar o lote não é opção.

### CH-04 — "Consulta de serviços tomados com timeout"

> ERPs de grandes tomadores reclamam de timeout em `ConsultarNfseServicoTomado`. O DBA diz que essa consulta está entre as que mais consomem banco no horário de pico. Alguém já tentou aumentar o timeout.

### CH-05 — "Cancelei a nota e a guia continua cobrando"

> O contribuinte cancelou uma NFS-e dentro do prazo e o portal mostra a nota como cancelada, mas a guia da competência continua cobrando o valor dela. No dia seguinte, depois de emitir outra nota, a guia "se corrigiu sozinha". Hoje o atendimento orienta: "espere emitir a próxima nota".

## Evolução: CNPJ alfanumérico

Desde julho de 2026, a Receita Federal pode emitir **CNPJs alfanuméricos** (IN RFB nº 2.229/2024). Prestadores e tomadores com o novo formato precisam funcionar em todo o fluxo: recepção e processamento de lotes, consultas e cancelamento. Os CNPJs numéricos continuam existindo, e **os clientes integrados não podem perceber nenhuma diferença** no que já funciona hoje.

Consulte a especificação oficial da Receita para a regra de cálculo dos dígitos verificadores.

## Revisão de código

Um colega abriu um PR com uma correção para parte dos problemas acima: veja `revisao/PR-118.md` e `revisao/PR-118.diff`. Revise como faria no GitHub e escreva sua revisão em `revisao/REVIEW-PR-118.md`. Diga se aprovaria, o que bloqueia o merge e por quê.

## O que entregar

Um **fork público** deste repositório, com histórico de commits que mostre como o trabalho evoluiu. Para cada chamado corrigido, queremos ver o teste falhando **antes** da correção e passando **depois**.

1. **Correções** no código .NET (C# e/ou VB.NET) e no banco (PL/SQL, DDL), na camada onde o problema realmente está. Mudanças de banco entram como scripts versionados em `db/migrations/`, aplicáveis em produção (19c, tabela grande, sistema no ar), com script de rollback quando fizer sentido.
2. **Um documento por chamado** em `docs/chamados/CH-0X.md`, escrito para o próximo plantonista: sintoma, como reproduzir, causa raiz, evidência (plano de execução, logs, medições), o que foi corrigido, qual teste prova a correção, impacto nos dados já gravados (e o script de correção de dados, se couber) e o risco da mudança.
3. **Testes de integração contra Oracle de verdade** (Testcontainers, compose, o que preferir). Não aceitamos mocks para o que depende do banco. Os testes precisam proteger o comportamento atual que os clientes usam, não só o que você mudou.
4. **CI** (GitHub Actions) rodando build `net48` + `net8.0` e todos os testes, inclusive os de integração.
5. **Evolução do CNPJ alfanumérico** implementada e testada.
6. **Revisão** em `revisao/REVIEW-PR-118.md`.
7. **README do fork** explicando: como rodar, em que ordem você atacou a fila e por quê, o que ficou de fora e o que você faria com mais tempo.

## O que olhamos

| Critério | Peso |
| --- | --- |
| Diagnóstico: causa raiz encontrada e provada com evidência, não com suposição | Alto |
| Correções na camada certa, respeitando as regras fiscais (nota imutável, numeração sem lacunas) | Alto |
| PL/SQL e SQL: leitura, depuração e otimização com plano de execução | Alto |
| Concorrência e processamento de lote no .NET tradicional | Alto |
| Testes de integração e não-regressão: o que é protegido e por quê | Alto |
| Impacto em dados já gravados e segurança das migrações em produção | Alto |
| Evolução do CNPJ alfanumérico sem quebrar quem já integra | Médio |
| Revisão do PR-118: o que você barra, o que aceita e como comunica | Médio |
| Documentação dos chamados: um plantonista entende sem te chamar? | Médio |

Para nós, um chamado bem resolvido, com causa raiz provada e teste que protege, vale mais do que cinco correções que "parecem funcionar".

## Diferenciais

- Rodar a suíte também em `net48` (Windows no CI)
- Medições antes/depois (memória, tempo, consumo do banco) reproduzíveis por script
- Um runbook de incidente para o plantão: o que olhar primeiro quando algo parecido acontecer
- Encontrar e documentar outros problemas que não estão na fila, com evidência

## FAQ

**Preciso de Windows?**
Não. Tudo compila e roda em Linux/macOS: os projetos `net48` compilam com reference assemblies e os testes rodam em `net8.0`.

**Preciso resolver tudo?**
Trate como uma semana real de sustentação: priorize, resolva com profundidade e explique a prioridade que escolheu. Se algo ficar de fora, diga o que é e como você atacaria.

**Posso reescrever o sistema em .NET 8?**
Não. Esta vaga é para manter vivo um sistema que não pode parar. Refatorar o necessário para corrigir e testar é bem-vindo. Reescrever não é o que buscamos aqui.

**Quanto de máquina preciso?**
O container Oracle usa cerca de 2–3 GB de RAM. A primeira subida leva alguns minutos por causa da carga de dados.

**Como será a entrevista?**
Vamos pedir que você explique e altere ao vivo partes da sua entrega.

---

Dúvidas: **selecao.pcrj@gmail.com**
