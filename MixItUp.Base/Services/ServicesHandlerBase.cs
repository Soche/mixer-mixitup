﻿using MixItUp.Base.Services.External;
using MixItUp.Base.Services.Mixer;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public abstract class ServicesHandlerBase
    {
        public IMixItUpService MixItUpService { get; protected set; }

        public IMixerStatusService MixerStatus { get; protected set; }

        public IUserService User { get; protected set; }

        public IChatService Chat { get; protected set; }

        public ISettingsService Settings { get; protected set; }
        public IFileService FileService { get; protected set; }
        public IAudioService AudioService { get; protected set; }
        public IInputService InputService { get; protected set; }
        public ITimerService TimerService { get; protected set; }
        public IGameQueueService GameQueueService { get; protected set; }
        public IImageManipulationService ImageManipulationService { get; protected set; }
        public ITextToSpeechService TextToSpeechService { get; protected set; }
        public ITranslationService TranslationService { get; protected set; }
        public IGiveawayService GiveawayService { get; protected set; }
        public ISerialService SerialService { get; protected set; }
        public LocalRemoteServiceBase RemoteService { get; protected set; }

        public IOverlayServiceManager OverlayServers { get; protected set; }
        public IStreamingSoftwareService OBSWebsocket { get; protected set; }
        public IStreamingSoftwareService StreamlabsOBSService { get; protected set; }
        public IStreamingSoftwareService XSplitServer { get; protected set; }
        public IDeveloperAPIService DeveloperAPI { get; protected set; }
        public IStreamlabsService Streamlabs { get; protected set; }
        public ITwitterService Twitter { get; protected set; }
        public IDiscordService Discord { get; protected set; }
        public ITiltifyService Tiltify { get; protected set; }
        public IExtraLifeService ExtraLife { get; protected set; }
        public ITelemetryService Telemetry { get; protected set; }
        public ITipeeeStreamService TipeeeStream { get; protected set; }
        public ITreatStreamService TreatStream { get; protected set; }
        public IStreamJarService StreamJar { get; protected set; }
        public IPatreonService Patreon { get; protected set; }
        public IOvrStreamService OvrStreamWebsocket { get; protected set; }
        public IIFTTTService IFTTT { get; protected set; }
        public IStreamlootsService Streamloots { get; protected set; }
        public IMixrElixrService MixrElixr { get; protected set; }
        public IJustGivingService JustGiving { get; protected set; }

        public abstract Task Close();

        public abstract Task<bool> InitializeOverlayServer();

        public abstract Task DisconnectOverlayServer();

        public abstract Task<bool> InitializeOBSWebsocket();
        public abstract Task DisconnectOBSStudio();

        public abstract Task<bool> InitializeStreamlabsOBSService();
        public abstract Task DisconnectStreamlabsOBSService();

        public abstract Task<bool> InitializeXSplitServer();
        public abstract Task DisconnectXSplitServer();

        public abstract Task<bool> InitializeDeveloperAPI();
        public abstract Task DisconnectDeveloperAPI();

        public abstract Task<bool> InitializeTelemetryService();
        public abstract Task DisconnectTelemetryService();

        public abstract Task<bool> InitializeTwitter();
        public abstract Task DisconnectTwitter();

        public abstract Task<bool> InitializeDiscord();
        public abstract Task DisconnectDiscord();

        public abstract Task<bool> InitializePatreon();
        public abstract Task DisconnectPatreon();

        public abstract Task<bool> InitializeOvrStream();
        public abstract Task DisconnectOvrStream();
    }
}
