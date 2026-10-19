using GerenciadorSalao.Models;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Data;

public static class DbInitializer
{
    public static void Inicializar(AppDbContext context)
    {
        // Garante que o banco de dados e as tabelas existam
        context.Database.EnsureCreated();

        // Seed inicial de Serviços caso o banco esteja vazio
        if (!context.Servicos.Any())
        {
            var servicosIniciais = new List<Servico>
            {
                new() { Descricao = "Corte Feminino", Valor = 80.00m, DuracaoMinutos = 45, Ativo = true },
                new() { Descricao = "Corte Masculino", Valor = 50.00m, DuracaoMinutos = 30, Ativo = true },
                new() { Descricao = "Manicure Completa", Valor = 40.00m, DuracaoMinutos = 40, Ativo = true },
                new() { Descricao = "Pedicure", Valor = 45.00m, DuracaoMinutos = 45, Ativo = true },
                new() { Descricao = "Escova e Modelagem", Valor = 60.00m, DuracaoMinutos = 40, Ativo = true },
                new() { Descricao = "Coloração / Tintura", Valor = 150.00m, DuracaoMinutos = 90, Ativo = true },
                new() { Descricao = "Hidratação Profunda", Valor = 90.00m, DuracaoMinutos = 50, Ativo = true },
                new() { Descricao = "Design de Sobrancelhas", Valor = 35.00m, DuracaoMinutos = 25, Ativo = true }
            };

            context.Servicos.AddRange(servicosIniciais);
        }

        // Seed inicial de Produtos caso o banco esteja vazio
        if (!context.Produtos.Any())
        {
            var produtosIniciais = new List<Produto>
            {
                new() { Descricao = "Shampoo nutritivo", Valor = 89.00m, Estoque = 15, Ativo = true },
                new() { Descricao = "Máscara de hidratação", Valor = 129.00m, Estoque = 8, Ativo = true }
            };

            context.Produtos.AddRange(produtosIniciais);
        }

        context.SaveChanges();

        // Seed inicial de Clientes e Agendamentos caso o banco não tenha clientes
        if (!context.Clientes.Any())
        {
            var c1 = new Cliente { Nome = "Mariana Oliveira", Telefone = "(11) 98765-4321", Observacao = "Prefere corte repicado" };
            var c2 = new Cliente { Nome = "Camila Santos", Telefone = "(11) 97654-3210", Observacao = "Alérgica a amônia" };
            var c3 = new Cliente { Nome = "Juliana Costa", Telefone = "(11) 96543-2109" };
            var c4 = new Cliente { Nome = "Ana Beatriz Lima", Telefone = "(11) 95432-1098" };
            var c5 = new Cliente { Nome = "Fernanda Ribeiro", Telefone = "(11) 94321-0987" };
            var c6 = new Cliente { Nome = "Carla Lima", Telefone = "(11) 93210-9876" };

            context.Clientes.AddRange(c1, c2, c3, c4, c5, c6);
            context.SaveChanges();

            // Seed de Agendamentos de Hoje (conforme prints de referência)
            var hoje = DateTime.Today;
            var servCorte = context.Servicos.FirstOrDefault(s => s.Descricao.Contains("Corte Feminino")) ?? context.Servicos.First();
            var servEscova = context.Servicos.FirstOrDefault(s => s.Descricao.Contains("Escova")) ?? context.Servicos.First();
            var servColor = context.Servicos.FirstOrDefault(s => s.Descricao.Contains("Coloração")) ?? context.Servicos.First();
            var servMani = context.Servicos.FirstOrDefault(s => s.Descricao.Contains("Manicure")) ?? context.Servicos.First();
            var servPedi = context.Servicos.FirstOrDefault(s => s.Descricao.Contains("Pedicure")) ?? context.Servicos.First();
            var servHidra = context.Servicos.FirstOrDefault(s => s.Descricao.Contains("Hidratação")) ?? context.Servicos.First();
            var servSobran = context.Servicos.FirstOrDefault(s => s.Descricao.Contains("Sobrancelhas")) ?? context.Servicos.First();

            var a1 = new Agendamento { ClienteId = c1.Id, DataHora = hoje.AddHours(9), TempoTotalPrevistoMinutos = 100, Status = StatusAgendamento.Pendente };
            a1.AgendamentoServicos.Add(new AgendamentoServico { Servico = servCorte });
            a1.AgendamentoServicos.Add(new AgendamentoServico { Servico = servEscova });

            var a2 = new Agendamento { ClienteId = c2.Id, DataHora = hoje.AddHours(10).AddMinutes(30), TempoTotalPrevistoMinutos = 120, Status = StatusAgendamento.Pendente };
            a2.AgendamentoServicos.Add(new AgendamentoServico { Servico = servColor });

            var a3 = new Agendamento { ClienteId = c3.Id, DataHora = hoje.AddHours(11), TempoTotalPrevistoMinutos = 60, Status = StatusAgendamento.Pendente };
            a3.AgendamentoServicos.Add(new AgendamentoServico { Servico = servMani });
            a3.AgendamentoServicos.Add(new AgendamentoServico { Servico = servPedi });

            var a4 = new Agendamento { ClienteId = c4.Id, DataHora = hoje.AddHours(14), TempoTotalPrevistoMinutos = 85, Status = StatusAgendamento.Pendente };
            a4.AgendamentoServicos.Add(new AgendamentoServico { Servico = servHidra });
            a4.AgendamentoServicos.Add(new AgendamentoServico { Servico = servEscova });

            var a5 = new Agendamento { ClienteId = c5.Id, DataHora = hoje.AddHours(14), TempoTotalPrevistoMinutos = 30, Status = StatusAgendamento.Pendente };
            a5.AgendamentoServicos.Add(new AgendamentoServico { Servico = servSobran });

            context.Agendamentos.AddRange(a1, a2, a3, a4, a5);

            // 2 Comandas abertas
            var cmdAberta1 = new Comanda { ClienteId = c1.Id, Status = StatusComanda.Aberta, DataAbertura = DateTime.Now.AddMinutes(-40), ValorTotal = 140.00m };
            var cmdAberta2 = new Comanda { ClienteId = c2.Id, Status = StatusComanda.Aberta, DataAbertura = DateTime.Now.AddMinutes(-20), ValorTotal = 150.00m };
            context.Comandas.AddRange(cmdAberta1, cmdAberta2);

            // Comandas finalizadas do ano para popular o Top 5 e o Acumulado Anual de R$ 24.900,00
            // 1. Mariana: R$ 5.700,00 (2 atendimentos)
            context.Comandas.Add(new Comanda { ClienteId = c1.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-3), DataFechamento = hoje.AddMonths(-3), ValorTotal = 3000.00m });
            context.Comandas.Add(new Comanda { ClienteId = c1.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-1), DataFechamento = hoje.AddMonths(-1), ValorTotal = 2700.00m });

            // 2. Fernanda: R$ 5.340,00 (2 atendimentos)
            context.Comandas.Add(new Comanda { ClienteId = c5.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-4), DataFechamento = hoje.AddMonths(-4), ValorTotal = 2800.00m });
            context.Comandas.Add(new Comanda { ClienteId = c5.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-2), DataFechamento = hoje.AddMonths(-2), ValorTotal = 2540.00m });

            // 3. Ana Beatriz: R$ 4.980,00 (2 atendimentos)
            context.Comandas.Add(new Comanda { ClienteId = c4.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-5), DataFechamento = hoje.AddMonths(-5), ValorTotal = 2500.00m });
            context.Comandas.Add(new Comanda { ClienteId = c4.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-2), DataFechamento = hoje.AddMonths(-2), ValorTotal = 2480.00m });

            // 4. Juliana: R$ 4.620,00 (2 atendimentos)
            context.Comandas.Add(new Comanda { ClienteId = c3.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-6), DataFechamento = hoje.AddMonths(-6), ValorTotal = 2400.00m });
            context.Comandas.Add(new Comanda { ClienteId = c3.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-1), DataFechamento = hoje.AddMonths(-1), ValorTotal = 2220.00m });

            // 5. Camila: R$ 4.260,00 (2 atendimentos)
            context.Comandas.Add(new Comanda { ClienteId = c2.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-7), DataFechamento = hoje.AddMonths(-7), ValorTotal = 2200.00m });
            context.Comandas.Add(new Comanda { ClienteId = c2.Id, Status = StatusComanda.Finalizada, DataAbertura = hoje.AddMonths(-3), DataFechamento = hoje.AddMonths(-3), ValorTotal = 2060.00m });

            context.SaveChanges();
        }
    }
}
