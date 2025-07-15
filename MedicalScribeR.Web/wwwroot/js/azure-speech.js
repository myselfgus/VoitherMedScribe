/**
 * Azure Speech Service Integration para MedicalScribeR - PRODUCTION READY
 * Implementa transcrição em tempo real usando Azure Speech SDK
 * Integrado com SignalR para comunicação em tempo real
 * 
 * @version 2.0.0
 * @author MedicalScribeR Team
 * @license MIT
 */

'use strict';

/**
 * Constantes de configuração
 * @readonly
 * @enum {number|string}
 */
const CONFIG = Object.freeze({
    CONFIDENCE_THRESHOLD: 0.7,
    MAX_RETRIES: 3,
    RETRY_DELAY_MS: 1000,
    RETRY_BACKOFF_MULTIPLIER: 2,
    TOKEN_REFRESH_INTERVAL_MS: 540000, // 9 minutes (tokens expire in 10)
    RECONNECT_INTERVALS_MS: [0, 2000, 10000, 30000],
    MAX_RECONNECT_ATTEMPTS: 5,
    CHUNK_DEBOUNCE_MS: 150,
    UI_UPDATE_THROTTLE_MS: 100,
    AUDIO_SAMPLE_RATE: 16000,
    SPEECH_TIMEOUT_MS: 500,
    MAX_CHUNK_LENGTH: 5000,
    WORKER_TIMEOUT_MS: 30000
});

/**
 * Códigos de erro customizados
 * @readonly
 * @enum {string}
 */
const ERROR_CODES = Object.freeze({
    BROWSER_NOT_SUPPORTED: 'BROWSER_NOT_SUPPORTED',
    MICROPHONE_PERMISSION_DENIED: 'MICROPHONE_PERMISSION_DENIED',
    SPEECH_SDK_NOT_LOADED: 'SPEECH_SDK_NOT_LOADED',
    TOKEN_ACQUISITION_FAILED: 'TOKEN_ACQUISITION_FAILED',
    SIGNALR_CONNECTION_FAILED: 'SIGNALR_CONNECTION_FAILED',
    SPEECH_RECOGNITION_FAILED: 'SPEECH_RECOGNITION_FAILED',
    SESSION_NOT_FOUND: 'SESSION_NOT_FOUND',
    NETWORK_ERROR: 'NETWORK_ERROR',
    TIMEOUT_ERROR: 'TIMEOUT_ERROR',
    VALIDATION_ERROR: 'VALIDATION_ERROR'
});

/**
 * Estados do serviço
 * @readonly
 * @enum {string}
 */
const SERVICE_STATES = Object.freeze({
    INITIALIZING: 'initializing',
    READY: 'ready',
    CONNECTING: 'connecting',
    CONNECTED: 'connected',
    RECORDING: 'recording',
    STOPPING: 'stopping',
    STOPPED: 'stopped',
    ERROR: 'error',
    DISPOSED: 'disposed'
});

/**
 * Classe principal do Azure Speech Service com melhorias de produção
 * @class AzureSpeechService
 */
class AzureSpeechService {
    /**
     * @constructor
     * @param {Object} options - Opções de configuração
     * @param {string} [options.language='pt-BR'] - Idioma de reconhecimento
     * @param {number} [options.confidenceThreshold=0.7] - Limite de confiança
     * @param {boolean} [options.enableLogging=false] - Habilitar logs detalhados
     */
    constructor(options = {}) {
        // Validação de entrada
        this._validateConstructorOptions(options);
        
        // Estado da aplicação
        this._state = SERVICE_STATES.INITIALIZING;
        this._currentSessionId = null;
        this._isRecording = false;
        this._isDisposed = false;
        
        // Configurações
        this._config = Object.freeze({
            language: options.language || 'pt-BR',
            confidenceThreshold: options.confidenceThreshold || CONFIG.CONFIDENCE_THRESHOLD,
            enableLogging: Boolean(options.enableLogging),
            enableDictation: true,
            enableProfanityFilter: false // Para uso médico
        });
        
        // Recursos do Azure
        this._speechRecognizer = null;
        this._audioConfig = null;
        this._speechConfig = null;
        this._token = null;
        this._region = null;
        this._tokenExpirationTimer = null;
        
        // Recursos de áudio
        this._mediaStream = null;
        this._audioContext = null;
        this._audioNodes = [];
        
        // SignalR
        this._signalRConnection = null;
        this._reconnectAttempts = 0;
        this._connectionPromise = null;
        
        // Event listeners e timers
        this._eventListeners = new Map();
        this._timers = new Set();
        this._debounceTimers = new Map();
        
        // Estatísticas e performance
        this._stats = {
            chunksProcessed: 0,
            totalWords: 0,
            averageConfidence: 0,
            startTime: null,
            lastChunkTime: null,
            errors: 0,
            reconnects: 0
        };
        
        // Worker para processamento pesado
        this._worker = null;
        this._workerTasks = new Map();
        this._taskCounter = 0;
        
        // Throttling/Debouncing
        this._updateTranscriptionThrottled = this._throttle(
            this._updateTranscriptionUI.bind(this), 
            CONFIG.UI_UPDATE_THROTTLE_MS
        );
        this._processChunkDebounced = this._debounce(
            this._processChunkInternal.bind(this),
            CONFIG.CHUNK_DEBOUNCE_MS
        );
        
        // Event emitter básico
        this._events = new Map();
        
        // Inicialização
        this._initialize();
    }

    /**
     * Valida opções do construtor
     * @private
     * @param {Object} options 
     */
    _validateConstructorOptions(options) {
        if (options && typeof options !== 'object') {
            throw new Error('Options must be an object');
        }
        
        if (options.language && typeof options.language !== 'string') {
            throw new Error('Language must be a string');
        }
        
        if (options.confidenceThreshold && 
            (typeof options.confidenceThreshold !== 'number' || 
             options.confidenceThreshold < 0 || 
             options.confidenceThreshold > 1)) {
            throw new Error('Confidence threshold must be a number between 0 and 1');
        }
    }

    /**
     * Inicialização assíncrona
     * @private
     */
    async _initialize() {
        try {
            this._log('info', 'Initializing Azure Speech Service...');
            
            // Verificar suporte do navegador
            await this._checkBrowserSupport();
            
            // Inicializar worker se disponível
            this._initializeWorker();
            
            // Configurar cleanup automático
            this._setupAutoCleanup();
            
            // Inicializar SignalR
            await this._initializeSignalR();
            
            this._state = SERVICE_STATES.READY;
            this._emit('ready');
            
            this._log('info', 'Azure Speech Service initialized successfully');
            
        } catch (error) {
            this._state = SERVICE_STATES.ERROR;
            this._handleError('Initialization failed', error, ERROR_CODES.SPEECH_SDK_NOT_LOADED);
        }
    }

    /**
     * Verifica suporte do navegador
     * @private
     * @returns {Promise<boolean>}
     */
    async _checkBrowserSupport() {
        const checks = [
            {
                condition: () => !!navigator.mediaDevices?.getUserMedia,
                error: 'Browser does not support getUserMedia'
            },
            {
                condition: () => !!(window.AudioContext || window.webkitAudioContext),
                error: 'Browser does not support Web Audio API'
            },
            {
                condition: () => typeof SpeechSDK !== 'undefined',
                error: 'Azure Speech SDK not loaded'
            },
            {
                condition: () => typeof signalR !== 'undefined',
                error: 'SignalR not loaded'
            },
            {
                condition: () => !!window.Promise,
                error: 'Browser does not support Promises'
            },
            {
                condition: () => !!window.fetch,
                error: 'Browser does not support Fetch API'
            }
        ];

        for (const check of checks) {
            if (!check.condition()) {
                throw new Error(check.error);
            }
        }

        // Verificar recursos avançados
        const hasAdvancedAudio = !!(navigator.mediaDevices.getSupportedConstraints?.() || {}).echoCancellation;
        if (!hasAdvancedAudio) {
            this._log('warn', 'Browser does not support advanced audio constraints');
        }

        return true;
    }

    /**
     * Inicializa worker para processamento pesado
     * @private
     */
    _initializeWorker() {
        if (typeof Worker === 'undefined') {
            this._log('warn', 'Web Workers not supported');
            return;
        }

        try {
            // Worker inline para processamento de chunks
            const workerCode = `
                self.onmessage = function(e) {
                    const { id, type, data } = e.data;
                    
                    try {
                        let result;
                        
                        switch(type) {
                            case 'processText':
                                result = processTextChunk(data);
                                break;
                            case 'calculateStats':
                                result = calculateStatistics(data);
                                break;
                            default:
                                throw new Error('Unknown task type: ' + type);
                        }
                        
                        self.postMessage({ id, success: true, result });
                    } catch (error) {
                        self.postMessage({ id, success: false, error: error.message });
                    }
                };
                
                function processTextChunk(chunk) {
                    const words = chunk.text.split(/\\s+/).filter(w => w.length > 0);
                    const wordCount = words.length;
                    const hasKeywords = /\\b(dor|medicamento|prescrição|diagnóstico|sintoma)\\b/i.test(chunk.text);
                    
                    return {
                        wordCount,
                        hasKeywords,
                        processedAt: Date.now()
                    };
                }
                
                function calculateStatistics(chunks) {
                    const totalWords = chunks.reduce((sum, chunk) => sum + (chunk.wordCount || 0), 0);
                    const avgConfidence = chunks.length > 0 
                        ? chunks.reduce((sum, chunk) => sum + chunk.confidence, 0) / chunks.length 
                        : 0;
                    
                    return {
                        totalWords,
                        avgConfidence,
                        chunkCount: chunks.length
                    };
                }
            `;

            const blob = new Blob([workerCode], { type: 'application/javascript' });
            this._worker = new Worker(URL.createObjectURL(blob));
            
            this._worker.onmessage = (e) => {
                const { id, success, result, error } = e.data;
                const task = this._workerTasks.get(id);
                
                if (task) {
                    this._workerTasks.delete(id);
                    clearTimeout(task.timeout);
                    
                    if (success) {
                        task.resolve(result);
                    } else {
                        task.reject(new Error(error));
                    }
                }
            };
            
            this._worker.onerror = (error) => {
                this._log('error', 'Worker error:', error);
            };
            
            this._log('info', 'Worker initialized successfully');
            
        } catch (error) {
            this._log('warn', 'Failed to initialize worker:', error);
        }
    }

    /**
     * Executa tarefa no worker
     * @private
     * @param {string} type - Tipo da tarefa
     * @param {*} data - Dados para processar
     * @returns {Promise<*>}
     */
    _runWorkerTask(type, data) {
        if (!this._worker) {
            // Fallback para execução local
            return Promise.resolve(this._processLocally(type, data));
        }

        return new Promise((resolve, reject) => {
            const id = ++this._taskCounter;
            
            const timeout = setTimeout(() => {
                this._workerTasks.delete(id);
                reject(new Error('Worker task timeout'));
            }, CONFIG.WORKER_TIMEOUT_MS);
            
            this._workerTasks.set(id, { resolve, reject, timeout });
            this._worker.postMessage({ id, type, data });
        });
    }

    /**
     * Fallback para processamento local
     * @private
     */
    _processLocally(type, data) {
        switch (type) {
            case 'processText':
                const words = data.text.split(/\s+/).filter(w => w.length > 0);
                return {
                    wordCount: words.length,
                    hasKeywords: /\b(dor|medicamento|prescrição|diagnóstico|sintoma)\b/i.test(data.text),
                    processedAt: Date.now()
                };
            default:
                return null;
        }
    }

    /**
     * Configura cleanup automático
     * @private
     */
    _setupAutoCleanup() {
        // Cleanup quando a página é fechada
        const cleanup = () => {
            if (!this._isDisposed) {
                this.dispose();
            }
        };

        ['beforeunload', 'unload', 'pagehide'].forEach(event => {
            window.addEventListener(event, cleanup, { passive: true });
            this._eventListeners.set(event, { target: window, handler: cleanup });
        });

        // Cleanup periódico de recursos não utilizados
        const periodicCleanup = () => {
            this._cleanupExpiredTokens();
            this._cleanupCompletedTasks();
        };

        const cleanupInterval = setInterval(periodicCleanup, 60000); // 1 minuto
        this._timers.add(cleanupInterval);
    }

    /**
     * Inicializa conexão SignalR com retry automático
     * @private
     * @returns {Promise<void>}
     */
    async _initializeSignalR() {
        if (this._connectionPromise) {
            return this._connectionPromise;
        }

        this._connectionPromise = this._connectSignalR();
        return this._connectionPromise;
    }

    /**
     * Conecta ao SignalR
     * @private
     * @returns {Promise<void>}
     */
    async _connectSignalR() {
        try {
            this._signalRConnection = new signalR.HubConnectionBuilder()
                .withUrl("/medicalhub", {
                    transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents,
                    logMessageContent: this._config.enableLogging,
                    logger: this._config.enableLogging ? signalR.LogLevel.Debug : signalR.LogLevel.Error
                })
                .withAutomaticReconnect(CONFIG.RECONNECT_INTERVALS_MS)
                .withHubProtocol(new signalR.JsonHubProtocol())
                .configureLogging(this._config.enableLogging ? signalR.LogLevel.Information : signalR.LogLevel.Error)
                .build();

            this._setupSignalREventHandlers();
            
            await this._signalRConnection.start();
            this._log('info', 'SignalR connection established');
            
            this._reconnectAttempts = 0;
            this._emit('signalr-connected');
            
        } catch (error) {
            this._handleError('SignalR connection failed', error, ERROR_CODES.SIGNALR_CONNECTION_FAILED);
            throw error;
        }
    }

    /**
     * Configura event handlers do SignalR
     * @private
     */
    _setupSignalREventHandlers() {
        const connection = this._signalRConnection;

        // Event handlers para mensagens do servidor
        const handlers = {
            'SessionStarted': (data) => this._onSessionStarted(data),
            'AgentActivated': (data) => this._onAgentActivated(data),
            'DocumentGenerated': (data) => this._onDocumentGenerated(data),
            'ProcessingCompleted': (data) => this._onProcessingCompleted(data),
            'ProcessingError': (data) => this._onProcessingError(data),
            'Error': (error) => this._onSignalRError(error),
            'TranscriptionUpdate': (data) => this._onTranscriptionUpdate(data)
        };

        Object.entries(handlers).forEach(([event, handler]) => {
            connection.on(event, handler);
        });

        // Handlers de conexão
        connection.onreconnected((connectionId) => {
            this._log('info', `SignalR reconnected: ${connectionId}`);
            this._reconnectAttempts = 0;
            this._showNotification("Conexão reestabelecida", "success");
            this._emit('signalr-reconnected', connectionId);
        });

        connection.onreconnecting((error) => {
            this._reconnectAttempts++;
            this._log('warn', `SignalR reconnecting (attempt ${this._reconnectAttempts}):`, error);
            this._showNotification("Tentando reconectar...", "warning");
            this._emit('signalr-reconnecting', { attempt: this._reconnectAttempts, error });
        });

        connection.onclose((error) => {
            this._log('error', 'SignalR connection closed:', error);
            
            if (this._reconnectAttempts >= CONFIG.MAX_RECONNECT_ATTEMPTS) {
                this._showError("Conexão perdida. Por favor, recarregue a página.");
                this._emit('signalr-failed');
            }
            
            this._emit('signalr-closed', error);
        });
    }

    /**
     * Obtém token de autenticação com retry automático
     * @private
     * @returns {Promise<{token: string, region: string}>}
     */
    async _getSpeechToken() {
        for (let attempt = 1; attempt <= CONFIG.MAX_RETRIES; attempt++) {
            try {
                this._log('debug', `Getting speech token (attempt ${attempt})`);
                
                const controller = new AbortController();
                const timeoutId = setTimeout(() => controller.abort(), 10000);
                
                const response = await fetch('/api/speech-token', {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json'
                    },
                    signal: controller.signal,
                    credentials: 'same-origin'
                });
                
                clearTimeout(timeoutId);

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const data = await response.json();
                
                if (!data.token || !data.region) {
                    throw new Error('Invalid token response format');
                }

                this._token = data.token;
                this._region = data.region;
                
                // Configurar renovação automática
                this._scheduleTokenRefresh();
                
                this._log('debug', 'Speech token acquired successfully');
                return { token: this._token, region: this._region };
                
            } catch (error) {
                const isLastAttempt = attempt === CONFIG.MAX_RETRIES;
                
                if (error.name === 'AbortError') {
                    this._log('warn', `Token request timeout (attempt ${attempt})`);
                } else {
                    this._log('warn', `Token acquisition failed (attempt ${attempt}):`, error);
                }

                if (isLastAttempt) {
                    throw new Error(`Failed to acquire speech token after ${CONFIG.MAX_RETRIES} attempts: ${error.message}`);
                }

                // Backoff exponencial
                const delay = CONFIG.RETRY_DELAY_MS * Math.pow(CONFIG.RETRY_BACKOFF_MULTIPLIER, attempt - 1);
                await this._sleep(delay);
            }
        }
    }

    /**
     * Agenda renovação automática do token
     * @private
     */
    _scheduleTokenRefresh() {
        if (this._tokenExpirationTimer) {
            clearTimeout(this._tokenExpirationTimer);
            this._timers.delete(this._tokenExpirationTimer);
        }

        this._tokenExpirationTimer = setTimeout(async () => {
            try {
                await this._getSpeechToken();
                this._log('debug', 'Token refreshed automatically');
            } catch (error) {
                this._log('error', 'Automatic token refresh failed:', error);
                this._emit('token-refresh-failed', error);
            }
        }, CONFIG.TOKEN_REFRESH_INTERVAL_MS);
        
        this._timers.add(this._tokenExpirationTimer);
    }

    /**
     * Solicita permissão de microfone com configurações otimizadas
     * @private
     * @returns {Promise<MediaStream>}
     */
    async _requestMicrophonePermission() {
        try {
            const constraints = {
                audio: {
                    echoCancellation: true,
                    noiseSuppression: true,
                    autoGainControl: true,
                    sampleRate: CONFIG.AUDIO_SAMPLE_RATE,
                    channelCount: 1,
                    latency: 0,
                    volume: 1.0
                }
            };

            // Verificar constraints suportadas
            const supportedConstraints = navigator.mediaDevices.getSupportedConstraints();
            Object.keys(constraints.audio).forEach(key => {
                if (!supportedConstraints[key]) {
                    this._log('warn', `Audio constraint '${key}' not supported`);
                    delete constraints.audio[key];
                }
            });

            this._mediaStream = await navigator.mediaDevices.getUserMedia(constraints);
            
            // Configurar áudio context para monitoramento
            this._setupAudioContext();
            
            this._log('info', 'Microphone permission granted');
            return this._mediaStream;
            
        } catch (error) {
            let errorMessage = 'Microphone permission denied';
            
            switch (error.name) {
                case 'NotAllowedError':
                    errorMessage = 'Permission to use microphone was denied';
                    break;
                case 'NotFoundError':
                    errorMessage = 'No microphone device found';
                    break;
                case 'NotReadableError':
                    errorMessage = 'Microphone is already in use by another application';
                    break;
                case 'OverconstrainedError':
                    errorMessage = 'Microphone does not support required audio constraints';
                    break;
                case 'SecurityError':
                    errorMessage = 'Microphone access blocked by security policy';
                    break;
            }
            
            throw new Error(errorMessage);
        }
    }

    /**
     * Configura audio context para monitoramento
     * @private
     */
    _setupAudioContext() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            this._audioContext = new AudioContext();
            
            const source = this._audioContext.createMediaStreamSource(this._mediaStream);
            const analyser = this._audioContext.createAnalyser();
            
            analyser.fftSize = 256;
            analyser.smoothingTimeConstant = 0.8;
            
            source.connect(analyser);
            this._audioNodes.push(source, analyser);
            
            // Monitorar nível de áudio
            this._monitorAudioLevel(analyser);
            
        } catch (error) {
            this._log('warn', 'Failed to setup audio context:', error);
        }
    }

    /**
     * Monitora nível de áudio
     * @private
     * @param {AnalyserNode} analyser 
     */
    _monitorAudioLevel(analyser) {
        const dataArray = new Uint8Array(analyser.frequencyBinCount);
        
        const checkLevel = () => {
            if (!this._isRecording || this._isDisposed) return;
            
            analyser.getByteFrequencyData(dataArray);
            const average = dataArray.reduce((sum, value) => sum + value, 0) / dataArray.length;
            
            this._emit('audio-level', average);
            
            requestAnimationFrame(checkLevel);
        };
        
        checkLevel();
    }

    /**
     * Inicia transcrição contínua
     * @public
     * @param {string} sessionId - ID da sessão
     * @param {Object} options - Opções de configuração
     * @returns {Promise<void>}
     */
    async startContinuousRecognition(sessionId, options = {}) {
        try {
            this._validateSessionId(sessionId);
            
            if (this._isRecording) {
                throw new Error('Recognition is already active');
            }

            if (this._state !== SERVICE_STATES.READY) {
                throw new Error(`Service not ready. Current state: ${this._state}`);
            }

            this._state = SERVICE_STATES.CONNECTING;
            this._currentSessionId = sessionId;
            
            this._log('info', `Starting recognition for session: ${sessionId}`);

            // Obter token se necessário
            if (!this._token) {
                await this._getSpeechToken();
            }

            // Solicitar permissão de microfone
            await this._requestMicrophonePermission();

            // Configurar Speech SDK
            await this._configureSpeechSDK(options);

            // Iniciar sessão SignalR
            await this._startSignalRSession(sessionId, options);

            // Iniciar reconhecimento
            await this._startSpeechRecognition();

            this._state = SERVICE_STATES.RECORDING;
            this._isRecording = true;
            this._stats.startTime = new Date();
            
            this._emit('recognition-started', { sessionId, options });
            this._log('info', 'Recognition started successfully');

        } catch (error) {
            this._state = SERVICE_STATES.ERROR;
            this._handleError('Failed to start recognition', error, ERROR_CODES.SPEECH_RECOGNITION_FAILED);
            throw error;
        }
    }

    /**
     * Configura o Speech SDK
     * @private
     * @param {Object} options - Opções de configuração
     */
    async _configureSpeechSDK(options) {
        try {
            this._speechConfig = SpeechSDK.SpeechConfig.fromAuthorizationToken(this._token, this._region);
            
            // Configurações básicas
            this._speechConfig.speechRecognitionLanguage = options.language || this._config.language;
            
            if (this._config.enableDictation) {
                this._speechConfig.enableDictation();
            }
            
            // Configurações avançadas de timeout
            this._speechConfig.setProperty(
                SpeechSDK.PropertyId.Speech_SegmentationSilenceTimeoutMs, 
                (options.silenceTimeout || CONFIG.SPEECH_TIMEOUT_MS).toString()
            );
            this._speechConfig.setProperty(
                SpeechSDK.PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, 
                (options.endSilenceTimeout || CONFIG.SPEECH_TIMEOUT_MS).toString()
            );
            
            // Configurações de logging
            if (this._config.enableLogging) {
                this._speechConfig.setProperty(
                    SpeechSDK.PropertyId.Speech_LogFilename, 
                    "AzureSpeechSDK.log"
                );
            }
            
            // Configurações de qualidade
            this._speechConfig.setProperty(
                SpeechSDK.PropertyId.SpeechServiceConnection_EnableAudioLogging,
                this._config.enableLogging.toString()
            );
            
            // Configurar entrada de áudio
            this._audioConfig = SpeechSDK.AudioConfig.fromDefaultMicrophoneInput();
            
            // Criar reconhecedor
            this._speechRecognizer = new SpeechSDK.SpeechRecognizer(this._speechConfig, this._audioConfig);
            
            this._setupSpeechEventHandlers();
            
        } catch (error) {
            throw new Error(`Failed to configure Speech SDK: ${error.message}`);
        }
    }

    /**
     * Configura event handlers do Speech SDK
     * @private
     */
    _setupSpeechEventHandlers() {
        if (!this._speechRecognizer) return;

        this._speechRecognizer.recognizing = (sender, e) => {
            this._onRecognizing(e);
        };

        this._speechRecognizer.recognized = (sender, e) => {
            this._onRecognized(e);
        };

        this._speechRecognizer.sessionStarted = (sender, e) => {
            this._log('info', 'Speech recognition session started');
            this._onRecognitionSessionStarted(e);
        };

        this._speechRecognizer.sessionStopped = (sender, e) => {
            this._log('info', 'Speech recognition session stopped');
            this._onRecognitionSessionStopped(e);
        };

        this._speechRecognizer.canceled = (sender, e) => {
            this._log('error', 'Speech recognition canceled:', e);
            this._onRecognitionCanceled(e);
        };
    }

    /**
     * Inicia reconhecimento de fala
     * @private
     * @returns {Promise<void>}
     */
    _startSpeechRecognition() {
        return new Promise((resolve, reject) => {
            this._speechRecognizer.startContinuousRecognitionAsync(
                () => {
                    this._log('info', 'Continuous recognition started');
                    resolve();
                },
                (error) => {
                    this._log('error', 'Failed to start recognition:', error);
                    reject(new Error(`Speech recognition failed: ${error}`));
                }
            );
        });
    }

    /**
     * Inicia sessão SignalR
     * @private
     * @param {string} sessionId 
     * @param {Object} options 
     */
    async _startSignalRSession(sessionId, options) {
        try {
            if (!this._signalRConnection || this._signalRConnection.state !== signalR.HubConnectionState.Connected) {
                await this._initializeSignalR();
            }

            const patientName = options.patientName || 
                               document.getElementById('patientName')?.value || 
                               'Paciente Anônimo';
            const consultationType = options.consultationType || 
                                   document.getElementById('consultationType')?.value || 
                                   'Consulta Geral';

            await this._signalRConnection.invoke("StartTranscription", sessionId, patientName, consultationType);
            this._log('info', `SignalR session started: ${sessionId}`);
            
        } catch (error) {
            throw new Error(`Failed to start SignalR session: ${error.message}`);
        }
    }

    /**
     * Para a transcrição
     */
    async stopRecording() {
        if (!this._isRecording || this._state !== SERVICE_STATES.RECORDING) {
            this._log('warn', 'No active recording to stop');
            return;
        }

        try {
            this._state = SERVICE_STATES.STOPPING;
            this._log('info', 'Stopping recording...');

            // Parar reconhecimento
            if (this._speechRecognizer) {
                await new Promise((resolve, reject) => {
                    this._speechRecognizer.stopContinuousRecognitionAsync(
                        () => {
                            this._log('info', 'Speech recognition stopped');
                            resolve();
                        },
                        (error) => {
                            this._log('error', 'Error stopping recognition:', error);
                            reject(new Error(`Failed to stop recognition: ${error}`));
                        }
                    );
                });
            }

            // Parar sessão SignalR
            await this._stopSignalRSession();

            // Cleanup de recursos
            this._cleanup();

            this._state = SERVICE_STATES.STOPPED;
            this._isRecording = false;
            
            this._emit('recognition-stopped');
            this._showNotification("Transcrição finalizada", "info");

        } catch (error) {
            this._state = SERVICE_STATES.ERROR;
            this._handleError('Failed to stop recording', error);
            throw error;
        }
    }

    /**
     * Para sessão SignalR
     * @private
     */
    async _stopSignalRSession() {
        try {
            if (this._signalRConnection && 
                this._signalRConnection.state === signalR.HubConnectionState.Connected && 
                this._currentSessionId) {
                
                await this._signalRConnection.invoke("StopTranscription", this._currentSessionId);
                this._log('info', 'SignalR session stopped');
            }
        } catch (error) {
            this._log('error', 'Error stopping SignalR session:', error);
        }
    }

    /**
     * Handler para texto sendo reconhecido (tempo real)
     * @private
     * @param {*} e - Event data
     */
    _onRecognizing(e) {
        if (e.result.reason === SpeechSDK.ResultReason.RecognizingSpeech) {
            const text = this._sanitizeText(e.result.text);
            if (text && text.trim().length > 0) {
                this._updateTranscriptionThrottled(text, true);
                this._emit('recognizing', { text, isInterim: true });
            }
        }
    }

    /**
     * Handler para texto reconhecido (final)
     * @private
     * @param {*} e - Event data
     */
    async _onRecognized(e) {
        if (e.result.reason === SpeechSDK.ResultReason.RecognizedSpeech) {
            const text = this._sanitizeText(e.result.text);
            const confidenceData = e.result.properties?.getProperty(
                SpeechSDK.PropertyId.SpeechServiceResponse_JsonResult
            );
            
            if (text && text.trim().length > 0) {
                const chunk = await this._createTranscriptionChunk(text, confidenceData);
                
                // Processar com debounce
                this._processChunkDebounced(chunk);
                
                this._emit('recognized', { chunk });
            }
        } else if (e.result.reason === SpeechSDK.ResultReason.NoMatch) {
            this._log('debug', 'No speech detected in audio segment');
            this._emit('no-speech');
        }
    }

    /**
     * Cria chunk de transcrição
     * @private
     * @param {string} text 
     * @param {string} confidenceData 
     * @returns {Promise<Object>}
     */
    async _createTranscriptionChunk(text, confidenceData) {
        const confidence = this._extractConfidence(confidenceData) || 0.8;
        const timestamp = new Date().toISOString();
        
        const chunk = {
            id: this._generateChunkId(),
            text: text,
            confidence: confidence,
            timestamp: timestamp,
            sequenceNumber: this._stats.chunksProcessed++,
            speaker: "Speaker1", // TODO: Implementar identificação de falantes
            sessionId: this._currentSessionId,
            length: text.length,
            wordCount: 0,
            hasKeywords: false
        };

        // Processar chunk no worker se disponível
        try {
            const processing = await this._runWorkerTask('processText', chunk);
            Object.assign(chunk, processing);
        } catch (error) {
            this._log('warn', 'Worker processing failed, using fallback:', error);
            const fallback = this._processLocally('processText', chunk);
            Object.assign(chunk, fallback);
        }

        this._updateStats(chunk);
        return chunk;
    }

    /**
     * Processa chunk internamente
     * @private
     * @param {Object} chunk 
     */
    async _processChunkInternal(chunk) {
        try {
            // Validar chunk
            if (!this._validateChunk(chunk)) {
                this._log('warn', 'Invalid chunk received:', chunk);
                return;
            }

            // Enviar para processamento via SignalR
            await this._sendChunkToSignalR(chunk);
            
            // Atualizar UI
            this._addTranscriptionChunk(chunk);
            this._clearTranscriptionPreview();
            
            this._emit('chunk-processed', chunk);
            
        } catch (error) {
            this._handleError('Failed to process chunk', error);
        }
    }

    /**
     * Valida chunk de transcrição
     * @private
     * @param {Object} chunk 
     * @returns {boolean}
     */
    _validateChunk(chunk) {
        const required = ['id', 'text', 'confidence', 'timestamp', 'sessionId'];
        
        for (const field of required) {
            if (!(field in chunk)) {
                this._log('error', `Missing required field: ${field}`);
                return false;
            }
        }

        if (typeof chunk.text !== 'string' || chunk.text.length === 0) {
            return false;
        }

        if (chunk.text.length > CONFIG.MAX_CHUNK_LENGTH) {
            this._log('warn', `Chunk text too long: ${chunk.text.length} characters`);
            return false;
        }

        if (typeof chunk.confidence !== 'number' || chunk.confidence < 0 || chunk.confidence > 1) {
            return false;
        }

        return true;
    }

    /**
     * Envia chunk para SignalR
     * @private
     * @param {Object} chunk 
     */
    async _sendChunkToSignalR(chunk) {
        if (!this._signalRConnection || 
            this._signalRConnection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('SignalR connection not available');
        }

        await this._signalRConnection.invoke("ProcessTranscriptionChunk", this._currentSessionId, chunk);
    }

    /**
     * Event handlers para SignalR
     */
    _onSessionStarted(data) {
        this._showNotification("Sessão iniciada com sucesso", "success");
        this._updateSessionInfo(data);
        this._emit('session-started', data);
    }

    _onAgentActivated(data) {
        this._updateAgentStatus(data.AgentName, "active", data.Status);
        this._showNotification(`Agente ${data.AgentName} ativado`, "info");
        this._emit('agent-activated', data);
    }

    _onDocumentGenerated(data) {
        this._addGeneratedDocument(data);
        this._showNotification(`Documento ${data.Type} gerado`, "success");
        this._emit('document-generated', data);
    }

    _onProcessingCompleted(data) {
        this._updateProcessingStats(data);
        this._emit('processing-completed', data);
    }

    _onProcessingError(data) {
        this._showError(`Erro no processamento: ${data.Error}`);
        this._emit('processing-error', data);
    }

    _onSignalRError(error) {
        this._handleError('SignalR error', error, ERROR_CODES.SIGNALR_CONNECTION_FAILED);
    }

    _onTranscriptionUpdate(data) {
        this._emit('transcription-update', data);
    }

    _onRecognitionSessionStarted(e) {
        this._updateUIStatus("recording");
        this._showNotification("Gravação iniciada", "success");
        this._emit('recognition-session-started', e);
    }

    _onRecognitionSessionStopped(e) {
        this._updateUIStatus("stopped");
        this._emit('recognition-session-stopped', e);
    }

    _onRecognitionCanceled(e) {
        const error = e.errorDetails || "Erro desconhecido";
        this._handleError(`Recognition canceled: ${error}`, e);
        this._cleanup();
    }

    /**
     * Funções de UI
     */
    _updateTranscriptionUI(text, isPreview = false) {
        try {
            if (isPreview) {
                this._updateTranscriptionPreview(text);
            } else {
                this._clearTranscriptionPreview();
            }
        } catch (error) {
            this._log('error', 'UI update failed:', error);
        }
    }

    _updateTranscriptionPreview(text) {
        const previewElement = document.getElementById('transcription-preview');
        if (previewElement) {
            previewElement.textContent = this._sanitizeText(text);
            previewElement.style.opacity = '0.7';
            previewElement.setAttribute('aria-live', 'polite');
        }
    }

    _clearTranscriptionPreview() {
        const previewElement = document.getElementById('transcription-preview');
        if (previewElement) {
            previewElement.textContent = '';
            previewElement.style.opacity = '';
        }
    }

    _addTranscriptionChunk(chunk) {
        const container = document.getElementById('transcriptionOutput');
        if (!container) return;

        // Remover placeholder se existir
        const placeholder = container.querySelector('.text-center');
        if (placeholder) {
            container.innerHTML = '';
        }

        const timestamp = new Date(chunk.timestamp).toLocaleTimeString();
        const confidenceClass = this._getConfidenceClass(chunk.confidence);

        const chunkDiv = document.createElement('div');
        chunkDiv.className = 'transcription-chunk mb-2 p-2 border-start border-primary border-3';
        chunkDiv.setAttribute('data-chunk-id', chunk.id);
        chunkDiv.setAttribute('role', 'log');
        chunkDiv.setAttribute('aria-live', 'polite');
        
        chunkDiv.innerHTML = `
            <div class="d-flex justify-content-between align-items-start">
                <div class="flex-grow-1">
                    <span class="text-muted small" aria-label="Timestamp">[${timestamp}]</span>
                    <span class="ms-2 chunk-text">${this._sanitizeHtml(chunk.text)}</span>
                </div>
                <small class="text-muted ${confidenceClass} ms-2" aria-label="Confidence">
                    ${Math.round(chunk.confidence * 100)}%
                </small>
            </div>
        `;

        container.appendChild(chunkDiv);
        
        // Scroll suave para o final
        this._scrollToBottom(container);
        
        // Manter apenas os últimos 100 chunks para performance
        this._limitTranscriptionChunks(container, 100);
    }

    _getConfidenceClass(confidence) {
        if (confidence > 0.8) return 'confidence-high text-success';
        if (confidence > 0.6) return 'confidence-medium text-warning';
        return 'confidence-low text-danger';
    }

    _scrollToBottom(container) {
        requestAnimationFrame(() => {
            container.scrollTo({
                top: container.scrollHeight,
                behavior: 'smooth'
            });
        });
    }

    _limitTranscriptionChunks(container, maxChunks) {
        const chunks = container.querySelectorAll('.transcription-chunk');
        if (chunks.length > maxChunks) {
            const toRemove = chunks.length - maxChunks;
            for (let i = 0; i < toRemove; i++) {
                chunks[i].remove();
            }
        }
    }

    _updateUIStatus(status) {
        const statusElement = document.getElementById('sessionStatus');
        const startBtn = document.getElementById('startBtn');
        const stopBtn = document.getElementById('stopBtn');

        const statusConfig = {
            recording: {
                class: 'badge bg-success',
                text: 'Gravando',
                startHidden: true,
                stopVisible: true
            },
            stopped: {
                class: 'badge bg-secondary',
                text: 'Parado',
                startHidden: false,
                stopVisible: false
            },
            error: {
                class: 'badge bg-danger',
                text: 'Erro',
                startHidden: false,
                stopVisible: false
            }
        };

        const config = statusConfig[status];
        if (!config) return;

        if (statusElement) {
            statusElement.className = config.class;
            statusElement.textContent = config.text;
        }

        if (startBtn) {
            startBtn.classList.toggle('d-none', config.startHidden);
            startBtn.disabled = status === 'error';
        }

        if (stopBtn) {
            stopBtn.classList.toggle('d-none', !config.stopVisible);
        }
    }

    _updateAgentStatus(agentName, status, message) {
        const agentElement = document.getElementById(`${agentName.toLowerCase()}-agent`);
        if (!agentElement) return;

        const badge = agentElement.querySelector('.badge');
        if (!badge) return;

        const statusConfig = {
            active: { class: 'badge bg-primary float-end', text: 'Ativo' },
            processing: { class: 'badge bg-warning float-end', text: 'Processando' },
            completed: { class: 'badge bg-success float-end', text: 'Concluído' },
            error: { class: 'badge bg-danger float-end', text: 'Erro' }
        };

        const config = statusConfig[status];
        if (config) {
            badge.className = config.class;
            badge.textContent = config.text;
            
            if (status === 'active') {
                agentElement.classList.add('active');
            }
        }

        // Acessibilidade
        agentElement.setAttribute('aria-label', `Agente ${agentName}: ${config?.text || status}`);
    }

    _addGeneratedDocument(document) {
        const container = document.getElementById('generatedDocuments');
        if (!container) return;

        // Remover placeholder se existir
        const placeholder = container.querySelector('.text-center');
        if (placeholder) {
            container.innerHTML = '';
        }

        const docDiv = document.createElement('div');
        docDiv.className = 'card mb-3';
        docDiv.setAttribute('role', 'article');
        docDiv.setAttribute('aria-label', `Documento ${document.Type}`);
        
        const truncatedContent = this._truncateText(document.Content, 200);
        const createdAt = new Date(document.CreatedAt).toLocaleString();
        
        docDiv.innerHTML = `
            <div class="card-header d-flex justify-content-between align-items-center">
                <h6 class="mb-0">
                    <i class="fas fa-file-medical" aria-hidden="true"></i> 
                    ${this._sanitizeHtml(document.Type)}
                </h6>
                <button class="btn btn-sm btn-outline-primary" 
                        onclick="downloadDocument('${document.DocumentId}')"
                        aria-label="Baixar documento ${document.Type}">
                    <i class="fas fa-download" aria-hidden="true"></i>
                </button>
            </div>
            <div class="card-body">
                <p class="card-text">${this._sanitizeHtml(truncatedContent)}</p>
                <small class="text-muted">
                    Gerado por: ${this._sanitizeHtml(document.GeneratedBy)} 
                    em <time datetime="${document.CreatedAt}">${createdAt}</time>
                </small>
            </div>
        `;

        container.appendChild(docDiv);
    }

    /**
     * Funções utilitárias
     */
    _generateChunkId() {
        return `chunk_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }

    _extractConfidence(confidenceJson) {
        if (!confidenceJson) return null;
        
        try {
            const parsed = JSON.parse(confidenceJson);
            return parsed.NBest?.[0]?.Confidence || null;
        } catch (error) {
            this._log('warn', 'Failed to parse confidence data:', error);
            return null;
        }
    }

    _updateStats(chunk) {
        this._stats.totalWords += chunk.wordCount || 0;
        this._stats.lastChunkTime = new Date();
        
        // Calcular confiança média
        const totalConfidence = this._stats.averageConfidence * (this._stats.chunksProcessed - 1) + chunk.confidence;
        this._stats.averageConfidence = totalConfidence / this._stats.chunksProcessed;
    }

    _sanitizeText(text) {
        if (typeof text !== 'string') return '';
        return text.trim().replace(/\s+/g, ' ');
    }

    _sanitizeHtml(text) {
        if (typeof text !== 'string') return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    _truncateText(text, maxLength) {
        if (!text || text.length <= maxLength) return text;
        return text.substring(0, maxLength) + '...';
    }

    _validateSessionId(sessionId) {
        if (!sessionId || typeof sessionId !== 'string') {
            throw new Error('Invalid session ID');
        }
        if (sessionId.length > 100) {
            throw new Error('Session ID too long');
        }
    }

    /**
     * Throttling e debouncing
     */
    _throttle(func, limit) {
        let inThrottle;
        return function(...args) {
            if (!inThrottle) {
                func.apply(this, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }

    _debounce(func, delay) {
        return (...args) => {
            const key = func.name || 'default';
            
            if (this._debounceTimers.has(key)) {
                clearTimeout(this._debounceTimers.get(key));
            }
            
            const timer = setTimeout(() => {
                this._debounceTimers.delete(key);
                func.apply(this, args);
            }, delay);
            
            this._debounceTimers.set(key, timer);
        };
    }

    /**
     * Event emitter
     */
    _emit(event, data) {
        const listeners = this._events.get(event) || [];
        listeners.forEach(listener => {
            try {
                listener(data);
            } catch (error) {
                this._log('error', `Event listener error for ${event}:`, error);
            }
        });
    }

    on(event, listener) {
        if (!this._events.has(event)) {
            this._events.set(event, []);
        }
        this._events.get(event).push(listener);
    }

    off(event, listener) {
        const listeners = this._events.get(event);
        if (listeners) {
            const index = listeners.indexOf(listener);
            if (index > -1) {
                listeners.splice(index, 1);
            }
        }
    }

    /**
     * Notificações
     */
    _showNotification(message, type = 'info') {
        try {
            // Usar toast do Bootstrap se disponível
            const toastElement = document.getElementById('notification-toast');
            const messageElement = document.getElementById('toast-message');
            
            if (toastElement && messageElement && typeof bootstrap !== 'undefined') {
                messageElement.textContent = message;
                toastElement.className = `toast text-bg-${type}`;
                
                const toast = new bootstrap.Toast(toastElement);
                toast.show();
            } else {
                // Fallback para console
                console.log(`[${type.toUpperCase()}] ${message}`);
            }
        } catch (error) {
            console.error('Failed to show notification:', error);
        }
    }

    _showError(message) {
        this._showNotification(message, 'danger');
        this._log('error', message);
    }

    /**
     * Error handling
     */
    _handleError(message, error, code = null) {
        this._stats.errors++;
        
        const errorInfo = {
            message,
            error: error?.message || error,
            code,
            timestamp: new Date().toISOString(),
            sessionId: this._currentSessionId,
            state: this._state
        };

        this._log('error', 'Error occurred:', errorInfo);
        this._emit('error', errorInfo);
        
        this._showError(message);
    }

    /**
     * Logging
     */
    _log(level, ...args) {
        if (!this._config.enableLogging && level === 'debug') return;
        
        const timestamp = new Date().toISOString();
        const prefix = `[${timestamp}] [AzureSpeech] [${level.toUpperCase()}]`;
        
        switch (level) {
            case 'error':
                console.error(prefix, ...args);
                break;
            case 'warn':
                console.warn(prefix, ...args);
                break;
            case 'info':
                console.info(prefix, ...args);
                break;
            case 'debug':
                console.debug(prefix, ...args);
                break;
            default:
                console.log(prefix, ...args);
        }
    }

    /**
     * Utility functions
     */
    _sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    _cleanupExpiredTokens() {
        // Implementar lógica de limpeza se necessário
    }

    _cleanupCompletedTasks() {
        // Limpar tarefas do worker que não foram removidas
        const now = Date.now();
        for (const [id, task] of this._workerTasks.entries()) {
            if (now - task.createdAt > CONFIG.WORKER_TIMEOUT_MS) {
                this._workerTasks.delete(id);
                clearTimeout(task.timeout);
            }
        }
    }

    /**
     * Cleanup de recursos
     * @private
     */
    _cleanup() {
        try {
            // Limpar Speech SDK
            if (this._speechRecognizer) {
                this._speechRecognizer.close();
                this._speechRecognizer = null;
            }

            if (this._speechConfig) {
                this._speechConfig.close();
                this._speechConfig = null;
            }

            if (this._audioConfig) {
                this._audioConfig.close();
                this._audioConfig = null;
            }

            // Limpar recursos de áudio
            if (this._mediaStream) {
                this._mediaStream.getTracks().forEach(track => track.stop());
                this._mediaStream = null;
            }

            this._audioNodes.forEach(node => {
                try {
                    if (node.disconnect) node.disconnect();
                } catch (e) {
                    // Ignorar erros de desconexão
                }
            });
            this._audioNodes = [];

            if (this._audioContext && this._audioContext.state !== 'closed') {
                this._audioContext.close().catch(() => {
                    // Ignorar erros de fechamento
                });
                this._audioContext = null;
            }

            // Limpar timers
            this._timers.forEach(timer => clearTimeout(timer));
            this._timers.clear();

            this._debounceTimers.forEach(timer => clearTimeout(timer));
            this._debounceTimers.clear();

            if (this._tokenExpirationTimer) {
                clearTimeout(this._tokenExpirationTimer);
                this._tokenExpirationTimer = null;
            }

            // Limpar event listeners
            this._eventListeners.forEach(({ target, handler }, event) => {
                target.removeEventListener(event, handler);
            });
            this._eventListeners.clear();

        } catch (error) {
            this._log('error', 'Cleanup error:', error);
        }
    }

    /**
     * Dispose completo
     * @public
     */
    dispose() {
        if (this._isDisposed) return;

        this._log('info', 'Disposing Azure Speech Service...');

        try {
            // Parar recording se ativo
            if (this._isRecording) {
                this.stopRecording().catch(() => {
                    // Ignorar erros durante dispose
                });
            }

            // Cleanup geral
            this._cleanup();

            // Fechar SignalR
            if (this._signalRConnection) {
                this._signalRConnection.stop().catch(() => {
                    // Ignorar erros de fechamento
                });
                this._signalRConnection = null;
            }

            // Terminar worker
            if (this._worker) {
                this._worker.terminate();
                this._worker = null;
            }

            // Limpar tarefas do worker
            this._workerTasks.clear();

            // Limpar eventos
            this._events.clear();

            this._state = SERVICE_STATES.DISPOSED;
            this._isDisposed = true;
            this._currentSessionId = null;

            this._log('info', 'Azure Speech Service disposed');

        } catch (error) {
            this._log('error', 'Dispose error:', error);
        }
    }

    /**
     * Getter para estatísticas
     * @public
     * @returns {Object}
     */
    getStats() {
        return {
            ...this._stats,
            duration: this._stats.startTime ? 
                (new Date() - this._stats.startTime) / 1000 : 0,
            isRecording: this._isRecording,
            state: this._state,
            hasToken: !!this._token,
            signalRConnected: this._signalRConnection?.state === signalR?.HubConnectionState?.Connected
        };
    }

    /**
     * Getter para estado atual
     * @public
     * @returns {string}
     */
    get state() {
        return this._state;
    }

    /**
     * Getter para status de recording
     * @public
     * @returns {boolean}
     */
    get isRecording() {
        return this._isRecording;
    }

    /**
     * Getter para session ID atual
     * @public
     * @returns {string|null}
     */
    get currentSessionId() {
        return this._currentSessionId;
    }
}

// Registrar classe globalmente
window.AzureSpeechService = AzureSpeechService;

// Instância global para compatibilidade
let globalSpeechService = null;

// Auto-inicialização quando DOM estiver pronto
document.addEventListener('DOMContentLoaded', function() {
    try {
        // Validar credenciais e endpoint
        const speechConfig = {
            subscriptionKey: window.azureSpeechKey || null,
            region: window.azureSpeechRegion || null
        };

        if (!speechConfig.subscriptionKey || !speechConfig.region) {
            throw new Error('Credenciais do Azure Speech Service estão ausentes ou inválidas.');
        }

        globalSpeechService = new AzureSpeechService({
            enableLogging: false, // Logging disabled for production
            subscriptionKey: speechConfig.subscriptionKey,
            region: speechConfig.region
        });
        
        // Disponibilizar globalmente
        window.speechService = globalSpeechService;
        
        console.log('Azure Speech Service initialized and ready');
        
    } catch (error) {
        console.error('Failed to initialize Azure Speech Service:', error);
        
        // Mostrar erro detalhado para o usuário
        const errorMsg = `Falha ao inicializar sistema de transcrição: ${error.message}`;
        setTimeout(() => {
            alert(errorMsg);
        }, 1000);
    }
});

// Cleanup automático na saída
window.addEventListener('beforeunload', function() {
    if (globalSpeechService && !globalSpeechService._isDisposed) {
        globalSpeechService.dispose();
    }
});
