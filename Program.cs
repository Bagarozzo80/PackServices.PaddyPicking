using Autofac;
using PackServices.ReadyPro.Data.Infrastructure;
using PackServices.ReadyPro.Data.Models;
using PackServices.ReadyPro.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace PaddyPicking
{
    static class Program
    {
        /// <summary>
        /// Punto di ingresso principale dell'applicazione.
        /// </summary>
        static void Main()
        {
            var container = BuildContainer();

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] { container.Resolve<PaddyService>() };
            ServiceBase.Run(ServicesToRun);
        }

        static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<PaddyService>().InstancePerLifetimeScope();

            //services
            builder.RegisterType<PaddyPickingService>().As<IPaddyPickingService>().InstancePerLifetimeScope();
            builder.RegisterType<DocumentService>().As<IDocumentService>().InstancePerLifetimeScope();

            //unit
            builder.RegisterType<UnitPaddyPicking>().As<IUnitPaddyPicking>().InstancePerLifetimeScope();
            builder.RegisterType<UnitDocument>().As<IUnitDocument>().InstancePerLifetimeScope();

            //model entities
            builder.RegisterType<ReadyProEntities>().InstancePerLifetimeScope();

            return builder.Build();
        }
    }
}
