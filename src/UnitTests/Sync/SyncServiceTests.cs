﻿using Common;
using Common.Database;
using Common.Dto;
using Common.Dto.Peloton;
using Common.Service;
using Conversion;
using FluentAssertions;
using Garmin;
using GitHub;
using GitHub.Dto;
using Moq;
using Moq.AutoMock;
using NUnit.Framework;
using Peloton;
using Sync;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnitTests.Sync
{
	public class SyncServiceTests
	{

		[Test]
		public async Task SyncAsync_When_PelotonStep_Fails_Should_ReturnCorrectResponse()
		{
			// SETUP
			var mocker = new AutoMocker();

			var service = mocker.CreateInstance<SyncService>();
			var peloton = mocker.GetMock<IPelotonService>();
			var db = mocker.GetMock<ISyncStatusDb>();
			var settingsService = mocker.GetMock<ISettingsService>();
			var settings = new Settings();
			settings.Format.Fit = true;
			settings.App.CheckForUpdates = false;
			settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);

			var syncStatus = new SyncServiceStatus();
			db.Setup(x => x.GetSyncStatusAsync()).Returns(Task.FromResult(syncStatus));
			peloton.Setup(x => x.GetRecentWorkoutsAsync(0)).Throws(new Exception());

			// ACT
			var response = await service.SyncAsync(0);

			// ASSERT
			response.SyncSuccess.Should().BeFalse();
			response.PelotonDownloadSuccess.Should().BeFalse();
			response.Errors.Should().NotBeNullOrEmpty();

			peloton.Verify(x => x.GetRecentWorkoutsAsync(0), Times.Once);
			db.Verify(x => x.UpsertSyncStatusAsync(It.IsAny<SyncServiceStatus>()), Times.Once);
		}

		[Test]
		public async Task SyncAsync_When_Conversion_StepFails_Should_ReturnCorrectResponse()
		{
			// SETUP
			var mocker = new AutoMocker();

			var config = new Settings();
			var service = mocker.CreateInstance<SyncService>();
			var peloton = mocker.GetMock<IPelotonService>();
			var db = mocker.GetMock<ISyncStatusDb>();
			var converter = mocker.GetMock<IConverter>();
			var settingsService = mocker.GetMock<ISettingsService>();
			var settings = new Settings();
			settings.Format.Fit = true;
			settings.App.CheckForUpdates = false;
			settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);

			var syncStatus = new SyncServiceStatus();
			db.Setup(x => x.GetSyncStatusAsync()).Returns(Task.FromResult(syncStatus));
			peloton.Setup(x => x.GetRecentWorkoutsAsync(0)).ReturnsAsync(new List<Workout>() { new Workout() { Status = "COMPLETE", Id = "1" } });
			peloton.Setup(x => x.GetWorkoutDetailsAsync(It.IsAny<ICollection<Workout>>())).ReturnsAsync(new P2GWorkout[] { new P2GWorkout() });
			converter.Setup(x => x.ConvertAsync(It.IsAny<P2GWorkout>())).Throws(new Exception());

			// ACT
			var response = await service.SyncAsync(0);

			// ASSERT
			response.SyncSuccess.Should().BeFalse();
			response.PelotonDownloadSuccess.Should().BeTrue();
			response.ConversionSuccess.Should().BeFalse();
			response.Errors.Should().NotBeNullOrEmpty();

			peloton.Verify(x => x.GetRecentWorkoutsAsync(0), Times.Once);
			converter.Verify(x => x.ConvertAsync(It.IsAny<P2GWorkout>()), Times.Once);
			db.Verify(x => x.UpsertSyncStatusAsync(It.IsAny<SyncServiceStatus>()), Times.Once);
		}

		[Test]
		public async Task SyncAsync_When_GarminUpload_StepFails_Should_ReturnCorrectResponse()
		{
			// SETUP
			var mocker = new AutoMocker();

			var service = mocker.CreateInstance<SyncService>();
			var peloton = mocker.GetMock<IPelotonService>();
			var db = mocker.GetMock<ISyncStatusDb>();
			var converter = mocker.GetMock<IConverter>();
			var garmin = mocker.GetMock<IGarminUploader>();
			var settingsService = mocker.GetMock<ISettingsService>();

			var settings = new Settings();
			settings.Format.Fit = true;
			settings.App.CheckForUpdates = false;
			settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);

			converter.Setup(c => c.ConvertAsync(It.IsAny<P2GWorkout>())).ReturnsAsync(new ConvertStatus() { Result = ConversionResult.Success });

			var syncStatus = new SyncServiceStatus();
			db.Setup(x => x.GetSyncStatusAsync()).Returns(Task.FromResult(syncStatus));
			peloton.Setup(x => x.GetRecentWorkoutsAsync(0)).ReturnsAsync(new List<Workout>() { new Workout() { Status = "COMPLETE", Id = "1" } });
			peloton.Setup(x => x.GetWorkoutDetailsAsync(It.IsAny<ICollection<Workout>>())).ReturnsAsync(new P2GWorkout[] { new P2GWorkout() });
			garmin.Setup(x => x.UploadToGarminAsync()).Throws(new Exception());

			// ACT
			var response = await service.SyncAsync(0);

			// ASSERT
			response.SyncSuccess.Should().BeFalse();
			response.PelotonDownloadSuccess.Should().BeTrue();
			response.ConversionSuccess.Should().BeTrue();
			response.UploadToGarminSuccess.Should().BeFalse();
			response.Errors.Should().NotBeNullOrEmpty();

			peloton.Verify(x => x.GetRecentWorkoutsAsync(0), Times.Once);
			converter.Verify(x => x.ConvertAsync(It.IsAny<P2GWorkout>()), Times.Once);
			garmin.Verify(x => x.UploadToGarminAsync(), Times.Once);
			db.Verify(x => x.UpsertSyncStatusAsync(It.IsAny<SyncServiceStatus>()), Times.Once);
		}

		[Test]
		public async Task SyncAsync_When_SyncSuccess_Should_ReturnCorrectResponse()
		{
			// SETUP
			var mocker = new AutoMocker();

			var service = mocker.CreateInstance<SyncService>();
			var peloton = mocker.GetMock<IPelotonService>();
			var db = mocker.GetMock<ISyncStatusDb>();
			var converter = mocker.GetMock<IConverter>();
			var garmin = mocker.GetMock<IGarminUploader>();
			var fileHandler = mocker.GetMock<IFileHandling>();
			var settingsService = mocker.GetMock<ISettingsService>();

			var settings = new Settings();
			settings.Format.Fit = true;
			settings.App.CheckForUpdates = false;
			settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);

			converter.Setup(c => c.ConvertAsync(It.IsAny<P2GWorkout>())).ReturnsAsync(new ConvertStatus() { Result = ConversionResult.Success });

			var syncStatus = new SyncServiceStatus();
			db.Setup(x => x.GetSyncStatusAsync()).Returns(Task.FromResult(syncStatus));
			peloton.Setup(x => x.GetRecentWorkoutsAsync(0)).ReturnsAsync(new List<Workout>() { new Workout() { Status = "COMPLETE", Id = "1" } });
			peloton.Setup(x => x.GetWorkoutDetailsAsync(It.IsAny<ICollection<Workout>>())).ReturnsAsync(new P2GWorkout[] { new P2GWorkout() });

			// ACT
			var response = await service.SyncAsync(0);

			// ASSERT
			response.SyncSuccess.Should().BeTrue();
			response.PelotonDownloadSuccess.Should().BeTrue();
			response.ConversionSuccess.Should().BeTrue();
			response.UploadToGarminSuccess.Should().BeTrue();
			response.Errors.Should().BeNullOrEmpty();

			peloton.Verify(x => x.GetRecentWorkoutsAsync(0), Times.Once);
			converter.Verify(x => x.ConvertAsync(It.IsAny<P2GWorkout>()), Times.Once);
			garmin.Verify(x => x.UploadToGarminAsync(), Times.Once);
			db.Verify(x => x.UpsertSyncStatusAsync(It.IsAny<SyncServiceStatus>()), Times.Once);
			fileHandler.Verify(x => x.Cleanup(It.IsAny<string>()), Times.Exactly(3));
		}

		[Test]
		public async Task SyncAsync_Should_ExcludeInProgress()
		{
			// SETUP
			var mocker = new AutoMocker();

			var service = mocker.CreateInstance<SyncService>();
			var peloton = mocker.GetMock<IPelotonService>();
			var db = mocker.GetMock<ISyncStatusDb>();
			var converter = mocker.GetMock<IConverter>();
			var garmin = mocker.GetMock<IGarminUploader>();
			var fileHandler = mocker.GetMock<IFileHandling>();
			var settingsService = mocker.GetMock<ISettingsService>();

			var settings = new Settings();
			settings.Format.Fit = true;
			settings.App.CheckForUpdates = false;
			settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);

			converter.Setup(c => c.ConvertAsync(It.IsAny<P2GWorkout>())).ReturnsAsync(new ConvertStatus() { Result = ConversionResult.Success });

			var syncStatus = new SyncServiceStatus();
			db.Setup(x => x.GetSyncStatusAsync()).Returns(Task.FromResult(syncStatus));
			peloton.Setup(x => x.GetWorkoutDetailsAsync(It.IsAny<ICollection<Workout>>())).ReturnsAsync(new P2GWorkout[] { new P2GWorkout() });

			peloton.Setup(x => x.GetRecentWorkoutsAsync(It.IsAny<int>()))
				.ReturnsAsync(new List<Workout>()
					{
						new Workout() { Status = "COMPLETE", Id = "1" },
						new Workout() { Status = "IN PROGRESS", Id = "2" }
					})
				.Verifiable();

			// ACT
			var response = await service.SyncAsync(0);

			// ASSERT
			response.SyncSuccess.Should().BeTrue();
			response.PelotonDownloadSuccess.Should().BeTrue();
			response.ConversionSuccess.Should().BeTrue();
			response.UploadToGarminSuccess.Should().BeTrue();
			response.Errors.Should().BeNullOrEmpty();

			peloton.Verify(x => x.GetRecentWorkoutsAsync(0), Times.Once);
			converter.Verify(x => x.ConvertAsync(It.IsAny<P2GWorkout>()), Times.Once);
			garmin.Verify(x => x.UploadToGarminAsync(), Times.Once);
			db.Verify(x => x.UpsertSyncStatusAsync(It.IsAny<SyncServiceStatus>()), Times.Once);
			fileHandler.Verify(x => x.Cleanup(It.IsAny<string>()), Times.Exactly(3));
		}

		[Test]
		public async Task SyncAsync_When_CheckForUpdates_Enabled_Should_Check()
		{
			// SETUP
			var mocker = new AutoMocker();

			var service = mocker.CreateInstance<SyncService>();
			var peloton = mocker.GetMock<IPelotonService>();
			var db = mocker.GetMock<ISyncStatusDb>();
			var settingsService = mocker.GetMock<ISettingsService>();
			var ghService = mocker.GetMock<IGitHubService>();
			
			var settings = new Settings();
			settings.App.CheckForUpdates = true;
			settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);

			var syncStatus = new SyncServiceStatus();
			db.Setup(x => x.GetSyncStatusAsync()).Returns(Task.FromResult(syncStatus));
			peloton.Setup(x => x.GetRecentWorkoutsAsync(0)).ReturnsAsync(new List<Workout>());

			ghService.Setup(x => x.GetLatestReleaseAsync())
				.ReturnsAsync(new P2GLatestRelease())
				.Verifiable();

			// ACT
			var response = await service.SyncAsync(0);

			// ASSERT
			ghService.Verify();
		}

		[Test]
		public async Task SyncAsync_When_CheckForUpdates_Disabled_Should_NotCheck()
		{
			// SETUP
			var mocker = new AutoMocker();

			var service = mocker.CreateInstance<SyncService>();
			var peloton = mocker.GetMock<IPelotonService>();
			var db = mocker.GetMock<ISyncStatusDb>();
			var settingsService = mocker.GetMock<ISettingsService>();
			var ghService = mocker.GetMock<IGitHubService>();

			var settings = new Settings();
			settings.App.CheckForUpdates = false;
			settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);

			var syncStatus = new SyncServiceStatus();
			db.Setup(x => x.GetSyncStatusAsync()).Returns(Task.FromResult(syncStatus));
			peloton.Setup(x => x.GetRecentWorkoutsAsync(0)).ReturnsAsync(new List<Workout>());

			ghService.Setup(x => x.GetLatestReleaseAsync())
				.ReturnsAsync(new P2GLatestRelease());

			// ACT
			var response = await service.SyncAsync(0);

			// ASSERT
			ghService.Verify(x => x.GetLatestReleaseAsync(), Times.Never);
		}
	}
}