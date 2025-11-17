using System;
using System.Collections.Generic;
using Xunit;
using Moq;
using Energy_Project.Models;
using Energy_Project.Services;
using Energy_Project.Services.Interfaces;
using System.Linq;

namespace SmartHomeTests
{
    public class DeviceServiceTests
    {
        private readonly Mock<IDeviceRepository> _deviceRepo;
        private readonly DeviceService _deviceService;

        public DeviceServiceTests()
        {
            _deviceRepo = new Mock<IDeviceRepository>();
            _deviceService = new DeviceService(_deviceRepo.Object);
        }

        /// <summary>
        /// Перевіряє, що пристрій вмикається, коли знайдено.
        /// </summary>
        [Fact]
        public void ToggleDevice_DeviceFound_TurnsOnSuccessfully()
        {
            var device = new Device { Id = 1, IsOn = false };
            _deviceRepo.Setup(repo => repo.GetById(1)).Returns(device);
            _deviceRepo.Setup(repo => repo.Update(device)).Verifiable();

            _deviceService.ToggleDevice(1, true);

            Assert.True(device.IsOn);
            _deviceRepo.Verify(repo => repo.Update(device), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що пристрій вимикається, коли знайдено.
        /// </summary>
        [Fact]
        public void ToggleDevice_DeviceFound_TurnsOffSuccessfully()
        {
            var device = new Device { Id = 1, IsOn = true };
            _deviceRepo.Setup(repo => repo.GetById(1)).Returns(device);
            _deviceRepo.Setup(repo => repo.Update(device)).Verifiable();

            _deviceService.ToggleDevice(1, false);

            Assert.False(device.IsOn);
            _deviceRepo.Verify(repo => repo.Update(device), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що викидається ArgumentException, якщо пристрій не знайдено.
        /// </summary>
        [Fact]
        public void ToggleDevice_DeviceNotFound_ThrowsArgumentException()
        {
            _deviceRepo.Setup(repo => repo.GetById(It.IsAny<int>())).Returns((Device)null!);

            var exception = Record.Exception(() => _deviceService.ToggleDevice(1, true));

            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            _deviceRepo.Verify(repo => repo.Update(It.IsAny<Device>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що метод репозиторію Update викликається рівно один раз при перемиканні пристрою.
        /// </summary>
        [Fact]
        public void ToggleDevice_UpdateCalledOnce()
        {
            var device = new Device { Id = 1, IsOn = false };
            _deviceRepo.Setup(repo => repo.GetById(1)).Returns(device);

            _deviceService.ToggleDevice(1, true);

            _deviceRepo.Verify(repo => repo.Update(device), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що повертається порожній список, якщо немає активних пристроїв.
        /// </summary>
        [Fact]
        public void GetActiveDevices_NoActiveDevices_ReturnsEmpty()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device> { new Device { IsOn = false } });

            var result = _deviceService.GetActiveDevices();

            Assert.Empty(result);
        }

        /// <summary>
        /// Перевіряє, що повертаються лише активні пристрої.
        /// </summary>
        [Fact]
        public void GetActiveDevices_SomeActiveDevices_ReturnsOnlyActive()
        {
            var devices = new List<Device>
            {
                new Device { IsOn = true, Id = 1 },
                new Device { IsOn = false, Id = 2 },
                new Device { IsOn = true, Id = 3 }
            };
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _deviceService.GetActiveDevices();

            Assert.Equal(2, result.Count());
            Assert.Contains(result, d => d.Id == 1);
            Assert.Contains(result, d => d.Id == 3);
            Assert.DoesNotContain(result, d => d.Id == 2);
        }

        /// <summary>
        /// Перевіряє, що повертаються всі пристрої, якщо всі активні.
        /// </summary>
        [Fact]
        public void GetActiveDevices_AllDevicesActive_ReturnsAll()
        {
            var devices = new List<Device>
            {
                new Device { IsOn = true, Id = 1 },
                new Device { IsOn = true, Id = 2 }
            };
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _deviceService.GetActiveDevices();

            Assert.Equal(2, result.Count());
            Assert.True(result.All(d => d.IsOn));
        }

        /// <summary>
        /// Перевіряє, що метод ToggleDevice повертає коректний стан пристрою.
        /// </summary>
        [Fact]
        public void ToggleDevice_DeviceFound_ReturnsCorrectState()
        {
            var device = new Device { Id = 1, IsOn = false };
            _deviceRepo.Setup(repo => repo.GetById(1)).Returns(device);

            var result = _deviceService.ToggleDevice(1, true);

            Assert.True(result);
            Assert.True(device.IsOn);
        }

        /// <summary>
        /// Перевіряє, що послідовні виклики ToggleDevice коректно оновлюють стан пристрою.
        /// </summary>
        [Fact]
        public void ToggleDevice_MultipleCalls_UpdatesCorrectly()
        {
            var device = new Device { Id = 1, IsOn = false };
            _deviceRepo.Setup(repo => repo.GetById(1)).Returns(device);

            _deviceService.ToggleDevice(1, true); // Turn on
            _deviceService.ToggleDevice(1, false); // Turn off

            Assert.False(device.IsOn);
            _deviceRepo.Verify(repo => repo.Update(device), Times.Exactly(2));
        }

        /// <summary>
        /// Перевіряє, що повернутий тип активних пристроїв є IEnumerable<Device>.
        /// </summary>
        [Fact]
        public void GetActiveDevices_ReturnsCorrectType()
        {
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(new List<Device>());

            var result = _deviceService.GetActiveDevices();

            Assert.IsAssignableFrom<IEnumerable<Device>>(result);
        }

        /// <summary>
        /// Перевіряє, що метод Update викликається, навіть якщо пристрій вже у потрібному стані.
        /// </summary>
        [Fact]
        public void ToggleDevice_ToggleSameState_StillUpdates()
        {
            var device = new Device { Id = 1, IsOn = true };
            _deviceRepo.Setup(repo => repo.GetById(1)).Returns(device);

            _deviceService.ToggleDevice(1, true);

            Assert.True(device.IsOn);
            _deviceRepo.Verify(repo => repo.Update(device), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що список активних пристроїв містить конкретний пристрій.
        /// </summary>
        [Fact]
        public void GetActiveDevices_ContainsSpecificActiveDevice()
        {
            var devices = new List<Device>
            {
                new Device { Id = 1, IsOn = true, Name = "Lamp" },
                new Device { Id = 2, IsOn = false, Name = "Fan" }
            };
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _deviceService.GetActiveDevices();

            Assert.Contains(result, d => d.Name == "Lamp");
        }

        /// <summary>
        /// Перевіряє, що список активних пристроїв не містить неактивний пристрій.
        /// </summary>
        [Fact]
        public void GetActiveDevices_DoesNotContainInactiveDevice()
        {
            var devices = new List<Device>
            {
                new Device { Id = 1, IsOn = true, Name = "Lamp" },
                new Device { Id = 2, IsOn = false, Name = "Fan" }
            };
            _deviceRepo.Setup(repo => repo.GetAll()).Returns(devices);

            var result = _deviceService.GetActiveDevices();

            Assert.DoesNotContain(result, d => d.Name == "Fan");
        }

        /// <summary>
        /// Перевіряє аргумент методу Update за допомогою предиката для мок-об'єкта.
        /// </summary>
        [Fact]
        public void ToggleDevice_VerifyPredicateForUpdate()
        {
            var device = new Device { Id = 1, IsOn = false, Name = "Heater" };
            _deviceRepo.Setup(repo => repo.GetById(1)).Returns(device);

            _deviceService.ToggleDevice(1, true);

            _deviceRepo.Verify(repo => repo.Update(It.Is<Device>(d => d.Id == 1 && d.IsOn == true && d.Name == "Heater")), Times.Once);
        }
    }
}
