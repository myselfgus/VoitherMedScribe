/**
 * Voither Medical Scribe - Integração Completa com Azure Healthcare Services
 * Sistema de transcrição médica com IA integrada
 */

class VoitherMedicalSystem {
    constructor() {
        this.connection = null;
        this.speechRecognizer = null;
        this.currentSessionId = null;
        this.isRecording = false;
        this.userId = null;
        this.userName = 'Dr. Usuário';
        this.healthBotConversationId = null;
        this.agents = {
            summary: { status: 'Inativo', lastActivity: null },
            prescription: { status: 'Inativo', lastActivity: null },
            action: { status: 'Inativo', lastActivity: null }
        };
        this.documents = [];
        this.transcriptionBuffer = '';
        
        // Configuração de endpoints
        this.endpoints = {
            speechToken: '/api/speech-token',
            transcription: '/api/transcription',
            healthcareAI: '/api/healthcareai',
            healthBot: '/api/healthbot',
            medicalHub: '/medicalhub'
        };
    }

    /**
     * Inicializa o sistema completo
     */
    async initialize(options = {}) {
        try {
            console.log('🚀 Inicializando Voither Medical System...');
            
            this.userId = options.userId || 'demo-user';
            this.userName = options.userName || 'Dr. Usuário';
            
            // Atualizar UI com informações do usuário
            this.updateUserInfo(this.userName);
            
            // Inicializar SignalR Hub
            await this.initializeSignalRHub();
            
            // Inicializar Health Bot
            await this.initializeHealthBot();
            
            // Gerar ID de sessão
            this.generateSessionId();
            
            // Configurar event listeners
            this.setupEventListeners();
            
            // Verificar saúde dos serviços
            await this.checkServicesHealth();
            
            console.log('✅ Sistema inicializado com sucesso');
            this.showNotification('Sistema carregado com sucesso', 'success');
            
        } catch (error) {
            console.error('❌ Erro na inicialização:', error);
            this.showNotification('Erro ao inicializar sistema', 'error');
        }
    }

    /**
     * Inicializa conexão SignalR com Medical Hub
     */
    async initializeSignalRHub() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(this.endpoints.medicalHub)
                .withAutomaticReconnect([0, 2000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Event handlers para respostas do hub
            this.connection.on("SessionStarted", (data) => {
                console.log('📝 Sessão iniciada:', data);
                this.showNotification(`Sessão ${data.SessionId} iniciada`, 'success');
            });

            this.connection.on("SessionStopped", (data) => {
                console.log('⏹️ Sessão parada:', data);
                this.showNotification('Sessão finalizada', 'info');
            });

            this.connection.on("TranscriptionUpdate", (data) => {
                console.log('🎤 Nova transcrição:', data);
                this.handleTranscriptionUpdate(data);
            });

            this.connection.on("AgentActivated", (data) => {
                console.log('🤖 Agente ativado:', data);
                this.handleAgentActivated(data);
            });

            this.connection.on("DocumentGenerated", (data) => {
                console.log('📄 Documento gerado:', data);
                this.handleDocumentGenerated(data);
            });

            this.connection.on("ActionItemGenerated", (data) => {
                console.log('✅ Ação gerada:', data);
                this.handleActionGenerated(data);
            });

            this.connection.on("ProcessingCompleted", (data) => {
                console.log('🎯 Processamento concluído:', data);
                this.showNotification(`Processamento concluído: ${data.DocumentsCount} documentos, ${data.ActionsCount} ações`, 'success');
            });

            this.connection.on("ProcessingError", (data) => {
                console.log('❌ Erro no processamento:', data);
                this.showNotification('Erro no processamento IA', 'error');
            });

            this.connection.on("Error", (message) => {
                console.error('❌ Erro do hub:', message);
                this.showNotification(message, 'error');
            });

            // Reconnection handlers
            this.connection.onreconnecting(error => {
                console.warn('🔄 Reconectando SignalR...', error);
                this.showNotification('Reconectando...', 'warning');
            });

            this.connection.onreconnected(connectionId => {
                console.log('🔗 SignalR reconectado:', connectionId);
                this.showNotification('Conexão reestabelecida', 'success');
            });

            this.connection.onclose(error => {
                console.error('💔 SignalR desconectado:', error);
                this.showNotification('Conexão perdida', 'error');
            });

            await this.connection.start();
            console.log('🔗 SignalR conectado');
            
        } catch (error) {
            console.error('❌ Erro ao conectar SignalR:', error);
            throw error;
        }
    }

    /**
     * Inicializa Azure Health Bot
     */
    async initializeHealthBot() {
        try {
            const response = await fetch(`${this.endpoints.healthBot}/start-conversation`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                },
                body: JSON.stringify({
                    userId: this.userId,
                    userName: this.userName
                })
            });

            if (response.ok) {
                const data = await response.json();
                this.healthBotConversationId = data.conversationId;
                console.log('🤖 Health Bot iniciado:', data);
                
                // Adicionar mensagem de boas-vindas
                this.addBotMessage(data.welcomeMessage || 'Olá! Como posso ajudar com esta consulta?', 'bot');
            } else {
                console.warn('⚠️ Health Bot indisponível');
                this.addBotMessage('Assistente temporariamente indisponível', 'system');
            }
        } catch (error) {
            console.warn('⚠️ Erro ao inicializar Health Bot:', error);
            this.addBotMessage('Assistente em modo offline', 'system');
        }
    }

    /**
     * Gera novo ID de sessão
     */
    generateSessionId() {
        const timestamp = new Date().toISOString().slice(0, 16).replace(/[-:]/g, '').replace('T', '-');
        this.currentSessionId = `SESS-${timestamp}`;
        
        const sessionIdElement = document.getElementById('sessionId');
        if (sessionIdElement) {
            sessionIdElement.value = this.currentSessionId;
        }
    }

    /**
     * Inicia sessão de transcrição
     */
    async startSession() {
        try {
            if (this.isRecording) {
                this.showNotification('Sessão já está ativa', 'warning');
                return;
            }

            console.log('🎬 Iniciando sessão de transcrição...');
            
            const patientName = document.getElementById('patientName')?.value || 'Paciente Anônimo';
            const consultationType = document.getElementById('consultationType')?.value || 'consulta-geral';
            
            // Inicializar reconhecimento de fala
            await this.initializeSpeechRecognition();
            
            // Iniciar sessão no hub
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                await this.connection.invoke("StartTranscription", this.currentSessionId, patientName, consultationType);
            }
            
            // Iniciar reconhecimento de fala
            if (this.speechRecognizer) {
                this.speechRecognizer.startContinuousRecognitionAsync();
                this.isRecording = true;
                
                // Atualizar UI
                this.updateRecordingUI(true);
                
                console.log('✅ Sessão de transcrição iniciada');
                this.showNotification('Transcrição iniciada com sucesso', 'success');
            }
            
        } catch (error) {
            console.error('❌ Erro ao iniciar sessão:', error);
            this.showNotification('Erro ao iniciar transcrição', 'error');
        }
    }

    /**
     * Para sessão de transcrição
     */
    async stopSession() {
        try {
            if (!this.isRecording) {
                this.showNotification('Nenhuma sessão ativa', 'warning');
                return;
            }

            console.log('⏹️ Parando sessão de transcrição...');
            
            this.isRecording = false;
            
            // Parar reconhecimento de fala
            if (this.speechRecognizer) {
                this.speechRecognizer.stopContinuousRecognitionAsync();
            }
            
            // Parar sessão no hub
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                await this.connection.invoke("StopTranscription", this.currentSessionId);
            }
            
            // Processar transcrição final com IA
            if (this.transcriptionBuffer) {
                await this.processWithHealthcareAI(this.transcriptionBuffer);
            }
            
            // Atualizar UI
            this.updateRecordingUI(false);
            
            console.log('✅ Sessão de transcrição parada');
            
        } catch (error) {
            console.error('❌ Erro ao parar sessão:', error);
            this.showNotification('Erro ao parar transcrição', 'error');
        }
    }

    /**
     * Inicializa Azure Speech Service
     */
    async initializeSpeechRecognition() {
        try {
            // Obter token do Azure Speech Service
            const tokenResponse = await fetch(this.endpoints.speechToken);
            if (!tokenResponse.ok) {
                throw new Error(`Erro ao obter token: ${tokenResponse.status}`);
            }
            
            const speechData = await tokenResponse.json();
            
            // Configurar Speech SDK
            const speechConfig = SpeechSDK.SpeechConfig.fromSubscription(speechData.token, speechData.region);
            speechConfig.speechRecognitionLanguage = 'pt-BR';
            speechConfig.setProperty(SpeechSDK.PropertyId.SpeechServiceConnection_SpeakerIdEnabled, "true");
            
            const audioConfig = SpeechSDK.AudioConfig.fromDefaultMicrophoneInput();
            this.speechRecognizer = new SpeechSDK.SpeechRecognizer(speechConfig, audioConfig);

            // Event handlers para reconhecimento
            this.speechRecognizer.recognized = (s, e) => {
                if (e.result.reason === SpeechSDK.ResultReason.RecognizedSpeech && e.result.text) {
                    const speakerId = e.result.properties.getProperty(SpeechSDK.PropertyId.SpeechServiceConnection_SpeakerId, "unknown");
                    this.processTranscriptionChunk(e.result.text, speakerId);
                }
            };

            this.speechRecognizer.sessionStarted = () => {
                console.log('🎤 Sessão de reconhecimento iniciada');
            };

            this.speechRecognizer.sessionStopped = () => {
                console.log('🎤 Sessão de reconhecimento parada');
            };

            console.log('✅ Azure Speech Service configurado');
            
        } catch (error) {
            console.error('❌ Erro ao configurar Speech Service:', error);
            throw error;
        }
    }

    /**
     * Processa chunk de transcrição
     */
    async processTranscriptionChunk(text, speakerId = 'unknown') {
        try {
            const chunk = {
                id: `chunk_${Date.now()}`,
                sessionId: this.currentSessionId,
                text: text,
                speaker: speakerId,
                timestamp: new Date().toISOString(),
                confidence: 0.95
            };

            // Adicionar ao buffer
            this.transcriptionBuffer += ` ${text}`;

            // Exibir na UI
            this.displayTranscriptionChunk(chunk);
            
            // Enviar para o hub para processamento IA
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                await this.connection.invoke("ProcessTranscriptionChunk", this.currentSessionId, chunk);
            }

            // Notificar Health Bot sobre nova transcrição
            if (this.healthBotConversationId && text.length > 10) {
                await this.notifyHealthBotOfTranscription(text);
            }
            
        } catch (error) {
            console.error('❌ Erro ao processar chunk:', error);
        }
    }

    /**
     * Processa texto com Azure Healthcare AI
     */
    async processWithHealthcareAI(text) {
        try {
            if (!text || text.length < 50) return;

            console.log('🧠 Processando com Healthcare AI...');
            
            const patientId = document.getElementById('patientName')?.value || 'PAT-001';
            
            const response = await fetch(`${this.endpoints.healthcareAI}/process-transcription`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                },
                body: JSON.stringify({
                    patientId: patientId,
                    transcriptionText: text,
                    sessionId: this.currentSessionId,
                    patientInfo: {
                        name: patientId,
                        age: null,
                        gender: null
                    }
                })
            });

            if (response.ok) {
                const result = await response.json();
                console.log('🎯 Resultado Healthcare AI:', result);
                this.showNotification('Análise de IA concluída', 'success');
            }
            
        } catch (error) {
            console.error('❌ Erro no Healthcare AI:', error);
        }
    }

    /**
     * Notifica Health Bot sobre transcrição
     */
    async notifyHealthBotOfTranscription(text) {
        try {
            if (!this.healthBotConversationId) return;

            const response = await fetch(`${this.endpoints.healthBot}/send-medical-context`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                },
                body: JSON.stringify({
                    conversationId: this.healthBotConversationId,
                    sessionId: this.currentSessionId,
                    transcriptionText: text,
                    consultationType: document.getElementById('consultationType')?.value || 'consulta-geral'
                })
            });

            if (response.ok) {
                const result = await response.json();
                if (result.messages && result.messages.length > 0) {
                    result.messages.forEach(msg => {
                        if (msg.from === 'bot') {
                            this.addBotMessage(msg.text, 'bot');
                        }
                    });
                }
            }
            
        } catch (error) {
            console.warn('⚠️ Erro ao notificar Health Bot:', error);
        }
    }

    /**
     * Envia mensagem para Health Bot
     */
    async sendBotMessage() {
        try {
            const input = document.getElementById('botInput');
            if (!input || !input.value.trim()) return;

            const message = input.value.trim();
            input.value = '';

            // Adicionar mensagem do usuário na UI
            this.addBotMessage(message, 'user');

            if (!this.healthBotConversationId) {
                this.addBotMessage('Assistente não disponível', 'system');
                return;
            }

            // Enviar para Health Bot
            const response = await fetch(`${this.endpoints.healthBot}/send-message`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                },
                body: JSON.stringify({
                    conversationId: this.healthBotConversationId,
                    message: message,
                    medicalContext: {
                        patientInfo: { name: document.getElementById('patientName')?.value },
                        consultationType: document.getElementById('consultationType')?.value,
                        currentSymptoms: this.transcriptionBuffer.slice(-500) // Últimos 500 chars
                    }
                })
            });

            if (response.ok) {
                const result = await response.json();
                if (result.messages && result.messages.length > 0) {
                    result.messages.forEach(msg => {
                        if (msg.from === 'bot') {
                            this.addBotMessage(msg.text, 'bot');
                        }
                    });
                }
            } else {
                this.addBotMessage('Erro na comunicação', 'system');
            }
            
        } catch (error) {
            console.error('❌ Erro ao enviar mensagem para bot:', error);
            this.addBotMessage('Erro interno', 'system');
        }
    }

    /**
     * Verifica saúde dos serviços
     */
    async checkServicesHealth() {
        try {
            const services = [
                { name: 'Healthcare AI', endpoint: `${this.endpoints.healthcareAI}/health` },
                { name: 'Health Bot', endpoint: `${this.endpoints.healthBot}/health` }
            ];

            for (const service of services) {
                try {
                    const response = await fetch(service.endpoint);
                    const status = response.ok ? '✅' : '❌';
                    console.log(`${status} ${service.name}: ${response.status}`);
                } catch (error) {
                    console.log(`❌ ${service.name}: Indisponível`);
                }
            }
            
        } catch (error) {
            console.warn('⚠️ Erro na verificação de saúde:', error);
        }
    }

    // ================================
    // EVENT HANDLERS
    // ================================

    handleTranscriptionUpdate(data) {
        this.displayTranscriptionChunk(data);
    }

    handleAgentActivated(data) {
        const agentElement = document.querySelector(`[data-agent="${data.AgentName}"]`);
        if (agentElement) {
            agentElement.classList.add('active');
            const badge = agentElement.querySelector('.badge');
            if (badge) {
                badge.className = 'badge bg-success';
                badge.textContent = 'Ativo';
            }
        }
        
        this.agents[data.AgentName.toLowerCase()] = {
            status: 'Ativo',
            lastActivity: new Date(),
            confidence: data.Confidence
        };
        
        this.showNotification(`Agente ${data.AgentName} ativado`, 'info');
    }

    handleDocumentGenerated(data) {
        this.documents.push(data);
        this.addGeneratedDocument(data);
        this.showNotification(`Documento ${data.Type} gerado`, 'success');
    }

    handleActionGenerated(data) {
        this.showNotification(`Nova ação: ${data.Title}`, 'info');
    }

    // ================================
    // UI METHODS
    // ================================

    displayTranscriptionChunk(chunk) {
        const container = document.getElementById('transcriptionOutput');
        if (!container) return;

        // Remover placeholder se existir
        const placeholder = container.querySelector('.text-center.text-muted');
        if (placeholder) {
            placeholder.remove();
        }

        const chunkElement = document.createElement('div');
        chunkElement.className = 'mb-2 p-2 border-l-4 border-blue-500';
        
        const timestamp = new Date(chunk.timestamp).toLocaleTimeString();
        const confidenceClass = chunk.confidence > 0.8 ? 'text-success' : 
                               chunk.confidence > 0.6 ? 'text-warning' : 'text-danger';
        
        chunkElement.innerHTML = `
            <div class="d-flex justify-content-between align-items-start">
                <div class="flex-grow-1">
                    <small class="text-muted">[${timestamp}] ${chunk.speaker}:</small>
                    <div class="text-dark">${chunk.text}</div>
                </div>
                <small class="fw-bold ${confidenceClass}">${Math.round(chunk.confidence * 100)}%</small>
            </div>
        `;
        
        container.appendChild(chunkElement);
        container.scrollTop = container.scrollHeight;
    }

    addBotMessage(text, from = 'bot') {
        const messagesContainer = document.getElementById('botMessages');
        if (!messagesContainer) return;

        const messageElement = document.createElement('div');
        messageElement.className = `bot-message ${from === 'user' ? 'sent' : ''}`;
        messageElement.textContent = text;
        
        messagesContainer.appendChild(messageElement);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    addGeneratedDocument(document) {
        const container = document.getElementById('generatedDocuments');
        if (!container) return;

        // Remover placeholder se existir
        const placeholder = container.querySelector('.text-center.text-muted');
        if (placeholder) {
            placeholder.remove();
        }

        const docElement = document.createElement('div');
        docElement.className = 'doc-card';
        docElement.innerHTML = `
            <div class="d-flex justify-content-between align-items-center">
                <div>
                    <strong>${document.type}</strong>
                    <br>
                    <small class="text-muted">Por: ${document.generatedBy}</small>
                </div>
                <div>
                    <button class="btn btn-sm btn-outline-primary" onclick="voitherSystem.downloadDocument('${document.documentId}')">
                        <i class="fas fa-download"></i>
                    </button>
                </div>
            </div>
        `;
        
        container.appendChild(docElement);
    }

    updateRecordingUI(isRecording) {
        const startBtn = document.getElementById('startBtn');
        const stopBtn = document.getElementById('stopBtn');
        
        if (startBtn && stopBtn) {
            if (isRecording) {
                startBtn.classList.add('d-none');
                stopBtn.classList.remove('d-none');
            } else {
                startBtn.classList.remove('d-none');
                stopBtn.classList.add('d-none');
            }
        }
    }

    updateUserInfo(userName) {
        const userNameElement = document.getElementById('userName');
        if (userNameElement) {
            userNameElement.innerHTML = `${userName} <span class="badge bg-secondary ms-1">Médico</span>`;
        }
    }

    showNotification(message, type = 'info') {
        try {
            const toastElement = document.getElementById('notification-toast');
            const messageElement = document.getElementById('toast-message');
            
            if (toastElement && messageElement) {
                messageElement.textContent = message;
                
                const icon = toastElement.querySelector('.fas');
                if (icon) {
                    const iconClasses = {
                        success: 'fas fa-check-circle text-success',
                        error: 'fas fa-exclamation-circle text-danger',
                        warning: 'fas fa-exclamation-triangle text-warning',
                        info: 'fas fa-info-circle text-info'
                    };
                    icon.className = iconClasses[type] || iconClasses.info;
                }
                
                const toast = new bootstrap.Toast(toastElement, {
                    autohide: true,
                    delay: type === 'error' ? 8000 : 4000
                });
                toast.show();
            }
        } catch (error) {
            console.error('Erro ao exibir notificação:', error);
            alert(`${type.toUpperCase()}: ${message}`);
        }
    }

    // ================================
    // UTILITY METHODS
    // ================================

    async getAuthToken() {
        // Em um cenário real, obter token de autenticação
        return 'demo-token';
    }

    setupEventListeners() {
        // Enter no input do bot
        const botInput = document.getElementById('botInput');
        if (botInput) {
            botInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') {
                    this.sendBotMessage();
                }
            });
        }

        // Atalhos de teclado
        document.addEventListener('keydown', (e) => {
            if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                e.preventDefault();
                if (this.isRecording) {
                    this.stopSession();
                } else {
                    this.startSession();
                }
            }
        });
    }

    async downloadDocument(documentId) {
        try {
            this.showNotification('Download iniciado', 'info');
            // Implementar download real
            console.log('Download documento:', documentId);
        } catch (error) {
            console.error('Erro no download:', error);
            this.showNotification('Erro no download', 'error');
        }
    }

    clearTranscription() {
        const container = document.getElementById('transcriptionOutput');
        if (container) {
            container.innerHTML = `
                <div class="text-center text-muted py-5">
                    <i class="fas fa-microphone-slash fa-3x mb-3"></i>
                    <p>Aguardando início da transcrição...</p>
                </div>
            `;
        }
        this.transcriptionBuffer = '';
        this.showNotification('Transcrição limpa', 'info');
    }

    exportTranscription() {
        if (!this.transcriptionBuffer) {
            this.showNotification('Nenhuma transcrição para exportar', 'warning');
            return;
        }

        const blob = new Blob([this.transcriptionBuffer], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `transcricao_${this.currentSessionId}.txt`;
        a.click();
        URL.revokeObjectURL(url);
        
        this.showNotification('Transcrição exportada', 'success');
    }

    performLogout() {
        this.showNotification('Logout realizado', 'info');
        // Implementar logout real
    }
}

// ================================
// GLOBAL INSTANCE & FUNCTIONS
// ================================

let voitherSystem = null;

// Inicialização quando DOM carregado
document.addEventListener('DOMContentLoaded', async function() {
    try {
        voitherSystem = new VoitherMedicalSystem();
        await voitherSystem.initialize({
            userId: 'user-123',
            userName: 'Dr. João Silva'
        });
    } catch (error) {
        console.error('Erro fatal na inicialização:', error);
    }
});

// Funções globais para compatibilidade com HTML
function startSession() {
    if (voitherSystem) voitherSystem.startSession();
}

function stopSession() {
    if (voitherSystem) voitherSystem.stopSession();
}

function sendBotMessage() {
    if (voitherSystem) voitherSystem.sendBotMessage();
}

function clearTranscription() {
    if (voitherSystem) voitherSystem.clearTranscription();
}

function exportTranscription() {
    if (voitherSystem) voitherSystem.exportTranscription();
}

function performLogout() {
    if (voitherSystem) voitherSystem.performLogout();
}
