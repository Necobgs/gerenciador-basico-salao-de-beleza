# Diretrizes de Projeto: Gerenciador de Salão de Beleza

Este documento estabelece as regras de arquitetura, padrões de design, convenções de código e restrições técnicas que devem ser estritamente seguidas ao desenvolver e refatorar o projeto.

---

## 1. Visão Geral e Princípios Fundamentais

- **Tipo de Projeto:** Aplicação Desktop local em **WPF** (.NET).
- **Abordagem Estrutural:** Projeto único (`GerenciadorSalao.csproj`). Não introduzir múltiplos projetos de biblioteca de classes prematuramente.
- **Filosofia:** *Simplicidade e Coesão*. Nada de microsserviços, message brokers (RabbitMQ), APIs remotas ou cache distribuído (Redis). Todo o sistema opera localmente em uma única máquina.

---

## 2. Estrutura de Pastas e Diretórios

Toda modificação ou adição de arquivos deve respeitar a hierarquia abaixo:

```text
GerenciadorSalao/
├── Data/                 # Contexto EF Core, configurações e migrations
│   ├── AppDbContext.cs
│   └── DbInitializer.cs
├── Models/               # Entidades de domínio (POCOs)
│   ├── Agendamento.cs
│   ├── Cliente.cs
│   └── Servico.cs
├── Services/             # Regras de negócio, cálculos e casos de uso
│   ├── AgendamentoService.cs
│   ├── ClienteService.cs
│   └── RelatorioService.cs
├── ViewModels/           # Lógica de apresentação, propriedades observáveis e comandos
│   ├── AgendamentosViewModel.cs
│   ├── ClientesViewModel.cs
│   ├── DashboardViewModel.cs
│   └── MainViewModel.cs
├── Views/                # Telas e UserControls em XAML (+ code-behind estritamente visual)
│   ├── AgendamentosView.xaml
│   ├── ClientesView.xaml
│   ├── DashboardView.xaml
│   └── MainWindow.xaml
├── Resources/            # Estilos globais, temas, imagens e ícones
│   ├── Images/
│   └── Styles/
├── Helpers/              # Utilitários compartilhados (converters, extensões reais)
├── App.xaml / App.xaml.cs
└── MainWindow.xaml / MainWindow.xaml.cs
```

---

## 3. Padrão Arquitetural: MVVM

O fluxo de comunicação deve ser estritamente unidirecional e desacoplado:

$$\text{View (XAML)} \longrightarrow \text{ViewModel} \longrightarrow \text{Services} \longrightarrow \text{Data (EF Core / SQLite)}$$

### Regras de Ouro:
1. **Regra de Isolamento da View:**
   - A `View` **nunca** acessa o banco de dados (`AppDbContext`) diretamente.
   - Code-behind (`.xaml.cs`) deve conter apenas lógica estritamente visual/comportamental da interface (animações, foco inicial). Toda manipulação de dados deve residir no `ViewModel`.
2. **Uso do CommunityToolkit.MVVM:**
   - Utilize geradores de código do toolkit (`[ObservableProperty]`, `[RelayCommand]`).
   - Evite boilerplate manual de `INotifyPropertyChanged`.
3. **Injeção de Dependência (DI):**
   - Registre serviços, ViewModels e `DbContext` no contêiner nativo (`Microsoft.Extensions.DependencyInjection`) configurado em `App.xaml.cs`.
   - Injetar dependências via construtor nos ViewModels e Services.

---

## 4. Persistência de Dados e SQLite

- **ORM:** Entity Framework Core (`Microsoft.EntityFrameworkCore.Sqlite`).
- **Localização do Banco:**
  - O banco de dados **não** deve ser salvo nas pastas de código, nem dentro de `bin/` ou `obj/`.
  - Caminho padrão de persistência:
    $$\%LOCALAPPDATA\% \setminus \text{GerenciadorSalao} \setminus \text{salao.db}$$
- **Inicialização:**
  - Aplicar migrations ou `EnsureCreated` de forma controlada na inicialização do sistema via `DbInitializer.cs`.

---

## 5. Bibliotecas e Dependências Homologadas

Ao adicionar pacotes, priorize:
- **UI:** `WPF-UI` (para interface moderna).
- **MVVM:** `CommunityToolkit.Mvvm`.
- **Banco de Dados:** `Microsoft.EntityFrameworkCore.Sqlite` e `Microsoft.EntityFrameworkCore.Design`.

*Nota:* Mantenha as versões dos pacotes alinhadas com a versão principal do SDK do projeto para evitar conflitos de runtime.

---

## 6. Tratamento de Erros e Validação

- **Regras de Negócio:** Centralizadas nos `Services` (ex.: validação de sobreposição de horários, regras de fidelidade).
- **Mensagens ao Usuário:** Mensagens de feedback devem ser claras, amigáveis e desprovidas de stack traces técnicos.
- **Validação de Formulários:** Validar no ViewModel antes de invocar o Service.