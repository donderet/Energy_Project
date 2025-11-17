using System;
using Xunit;
using Energy_Project.Services.Interfaces;
using Moq;
using Energy_Project.Models;
using Energy_Project.Services;

namespace SmartHomeTests
{
    public class EnergyMonitorServiceTests
    {
        private readonly Mock<IDeviceRepository> _deviceRepo;
        private readonly Mock<IEnergyPlanRepository> _planRepo;
        private readonly Mock<INotificationService> _notify;
        private readonly EnergyMonitorService _energyMonitorService;

        public EnergyMonitorServiceTests()
        {
            _deviceRepo = new Mock<IDeviceRepository>();
            _planRepo = new Mock<IEnergyPlanRepository>();
            _notify = new Mock<INotificationService>();
            _energyMonitorService = new EnergyMonitorService(_deviceRepo.Object, _planRepo.Object, _notify.Object);
        }

        /// <summary>
        /// Перевіряє, що якщо немає активних пристроїв, споживання енергії дорівнює нулю.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_NoActiveDevices_ReturnsZero()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device>());

            var result = _energyMonitorService.CalculateCurrentUsageKwh();

            Assert.Equal(0, result);
        }

        /// <summary>
        /// Перевіряє, що метод коректно розраховує загальне споживання від активних пристроїв.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_ActiveDevices_ReturnsCorrectUsage()
        {
            var devices = new List<Device>
            {
                new Device { IsOn = true, PowerUsageWatts = 1000 },
                new Device { IsOn = true, PowerUsageWatts = 500 },
                new Device { IsOn = false, PowerUsageWatts = 2000 }
            };
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _energyMonitorService.CalculateCurrentUsageKwh();

            Assert.Equal(1.5, result); // (1000 + 500) / 1000 = 1.5
        }

        /// <summary>
        /// Перевіряє, що сповіщення про перевантаження не надсилається, якщо споживання нижче ліміту.
        /// </summary>
        [Fact]
        public void CheckForOverload_UsageBelowLimit_NoAlertSent()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device> { new Device { IsOn = true, PowerUsageWatts = 100 } });
            _planRepo.Setup(repo => repo.GetCurrentPlan()).Returns(new EnergyPlan { DailyLimitKwh = 1.0 });

            _energyMonitorService.CheckForOverload();

            _notify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що сповіщення про перевантаження надсилається, якщо споживання перевищує ліміт.
        /// </summary>
        [Fact]
        public void CheckForOverload_UsageAboveLimit_AlertSent()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device> { new Device { IsOn = true, PowerUsageWatts = 2000 } });
            _planRepo.Setup(repo => repo.GetCurrentPlan()).Returns(new EnergyPlan { DailyLimitKwh = 1.0 });

            _energyMonitorService.CheckForOverload();

            _notify.Verify(n => n.SendAlert("Overload detected: 2 kWh used!"), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що метод UpdateEnergyLimit коректно оновлює ліміт та викликає збереження плану.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_ValidLimit_UpdatesPlan()
        {
            var plan = new EnergyPlan { DailyLimitKwh = 5.0 };
            _planRepo.Setup(repo => repo.GetCurrentPlan()).Returns(plan);

            _energyMonitorService.UpdateEnergyLimit(10.0);

            Assert.Equal(10.0, plan.DailyLimitKwh);
            _planRepo.Verify(repo => repo.UpdatePlan(plan), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що спроба встановити від'ємний ліміт викидає ArgumentOutOfRangeException.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_NegativeLimit_ThrowsArgumentException()
        {
            var plan = new EnergyPlan { DailyLimitKwh = 5.0 };
            _planRepo.Setup(repo => repo.GetCurrentPlan()).Returns(plan);

            var exception = Record.Exception(() => _energyMonitorService.UpdateEnergyLimit(-1.0));

            Assert.NotNull(exception);
            Assert.IsType<ArgumentOutOfRangeException>(exception);
        }

        /// <summary>
        /// Додаткова перевірка коректності сумування споживання для кількох активних пристроїв.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_MultipleDevices_SumsCorrectly()
        {
            var devices = new List<Device>
            {
                new Device { IsOn = true, PowerUsageWatts = 1000 },
                new Device { IsOn = true, PowerUsageWatts = 500 },
                new Device { IsOn = true, PowerUsageWatts = 250 },
            };
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _energyMonitorService.CalculateCurrentUsageKwh();

            Assert.Equal(1.75, result); // (1000 + 500 + 250) / 1000 = 1.75
        }

        /// <summary>
        /// Перевіряє, що відсутність поточного плану призводить до NullReferenceException.
        /// </summary>
        [Fact]
        public void CheckForOverload_NoCurrentPlan_ThrowsException()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device> { new Device { IsOn = true, PowerUsageWatts = 100 } });
            _planRepo.Setup(repo => repo.GetCurrentPlan()).Returns((EnergyPlan)null!);

            var exception = Record.Exception(() => _energyMonitorService.CheckForOverload());

            Assert.NotNull(exception);
            Assert.IsType<NullReferenceException>(exception);
        }

        /// <summary>
        /// Перевіряє, що сповіщення не надсилається, якщо споживання точно дорівнює ліміту.
        /// </summary>
        [Fact]
        public void CheckForOverload_UsageExactlyAtLimit_NoAlertSent()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device> { new Device { IsOn = true, PowerUsageWatts = 1000 } });
            _planRepo.Setup(repo => repo.GetCurrentPlan()).Returns(new EnergyPlan { DailyLimitKwh = 1.0 });

            _energyMonitorService.CheckForOverload();

            _notify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє можливість встановлення нульового ліміту енергії та збереження плану.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_ZeroLimit_UpdatesPlan()
        {
            var plan = new EnergyPlan { DailyLimitKwh = 5.0 };
            _planRepo.Setup(repo => repo.GetCurrentPlan()).Returns(plan);

            _energyMonitorService.UpdateEnergyLimit(0.0);

            Assert.Equal(0.0, plan.DailyLimitKwh);
            _planRepo.Verify(repo => repo.UpdatePlan(plan), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що список пристроїв, що використовуються для розрахунку, містить певний елемент.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_ContainsSpecificDevice_ReturnsCorrectUsage()
        {
            var devices = new List<Device>
            {
                new Device { IsOn = true, PowerUsageWatts = 1000, Name = "Fridge" },
                new Device { IsOn = true, PowerUsageWatts = 500, Name = "TV" }
            };
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _energyMonitorService.CalculateCurrentUsageKwh();

            Assert.Equal(1.5, result);
            Assert.Contains(devices, d => d.Name == "Fridge");
        }

        /// <summary>
        /// Перевіряє, що коли список пристроїв порожній, споживання дорівнює нулю.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_EmptyDevicesList_ReturnsZero()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device>());

            var result = _energyMonitorService.CalculateCurrentUsageKwh();

            Assert.Empty(_deviceRepo.Object.GetAll());
            Assert.Equal(0, result);
        }

        /// <summary>
        /// Перевіряє, що коли список пристроїв не порожній, споживання не дорівнює нулю.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_NotEmptyDevicesList_ReturnsNonZero()
        {
            var devices = new List<Device>
            {
                new Device { IsOn = true, PowerUsageWatts = 100 },
            };
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _energyMonitorService.CalculateCurrentUsageKwh();

            Assert.NotEmpty(_deviceRepo.Object.GetAll());
            Assert.NotEqual(0, result);
        }
        
        /// <summary>
        /// Перевіряє, що сповіщення про перевантаження надсилається з повідомленням, яке відповідає складному предикату.
        /// </summary>
        [Fact]
        public void CheckForOverload_ComplexPredicate_AlertSent()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device> { new Device { IsOn = true, PowerUsageWatts = 2500 } });
            _planRepo.Setup(repo => repo.GetCurrentPlan()).Returns(new EnergyPlan { DailyLimitKwh = 2.0 });

            _energyMonitorService.CheckForOverload();

            _notify.Verify(n => n.SendAlert(It.Is<string>(s => s.Contains("Overload") && s.Contains("kWh"))), Times.Once);
        }

        /// <summary>
        /// Параметризований тест, який перевіряє розрахунок споживання для різних наборів даних пристроїв.
        /// </summary>
        [Theory]
        [InlineData(new double[] { 1000, 500 }, new bool[] { true, true }, 1.5)] // Two active devices
        [InlineData(new double[] { 1000, 500 }, new bool[] { true, false }, 1.0)] // One active, one inactive
        [InlineData(new double[] { }, new bool[] { }, 0.0)] // No devices
        [InlineData(new double[] { 2000 }, new bool[] { true }, 2.0)] // One active device
        public void CalculateCurrentUsageKwh_Parameterized_ReturnsCorrectUsage(double[] powerUsages, bool[] isOnStates, double expectedUsage)
        {
            var devices = new List<Device>();
            for (int i = 0; i < powerUsages.Length; i++)
            {
                devices.Add(new Device { IsOn = isOnStates[i], PowerUsageWatts = powerUsages[i] });
            }
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _energyMonitorService.CalculateCurrentUsageKwh();

            Assert.Equal(expectedUsage, result);
        }

        /// <summary>
        /// Перевіряє, що сповіщення надсилається точно кілька разів при послідовних перевантаженнях.
        /// </summary>
        [Fact]
        public void CheckForOverload_MultipleOverloads_AlertSentMultipleTimes()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device> { new Device { IsOn = true, PowerUsageWatts = 1500 } });
            _planRepo.SetupSequence(repo => repo.GetCurrentPlan())
                .Returns(new EnergyPlan { DailyLimitKwh = 1.0 })
                .Returns(new EnergyPlan { DailyLimitKwh = 0.5 });

            _energyMonitorService.CheckForOverload();
            _energyMonitorService.CheckForOverload();

            _notify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Exactly(2));
        }
    }
}
