using Bindito.Core;

namespace Jove.SurvivalBrief
{
    [Context("Game")]
    public class TimerConfigurator : Configurator
    {
        protected override void Configure()
        {
            Bind<WeatherCountdown>().AsSingleton();
            Bind<ConsumptionModel>().AsSingleton();
            Bind<ConsumptionPanel>().AsSingleton();
        }
    }
}
