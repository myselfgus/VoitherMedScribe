/**
 * OpenAI Whisper Transcription Service para MedicalScribeR
 * Implementa transcrição de áudio usando OpenAI Whisper API
 * Suporta gravações longas (90min+) com chunking inteligente e diarização
 * 
 * @version 3.0.0
 * @author MedicalScribeR Team
 */

'use strict';

class WhisperTranscriptionService {
    constructor(options = {}) {
        this.config = {
            endpoint: options.endpoint || 'https://medicalscribe-openai-eastus2.openai.azure.com/',
            apiKey: options.apiKey || '',
            deploymentName: 'whisper',
            language: options.language || 'pt',
            enableDiarization: options.enableDiarization !== false,
            chunkSize: options.chunkSize || 25 * 1024 * 1024, // 25MB chunks
            maxDuration: options.maxDuration || 5400, // 90 minutes
            enableTimestamps: true,
            responseFormat: 'verbose_json'
        };
        
        this.mediaRecorder = null;
        this.audioChunks = [];
        this.isRecording = false;
        this.sessionId = null;
        this.signalRConnection = null;
        
        // Event listeners
        this.events = new Map();
        
        this.initializeSignalR();
    }

    /**
     * Inicializar conexão SignalR
     */
    async initializeSignalR() {
        try {
            this.signalRConnection = new signalR.HubConnectionBuilder()
                .withUrl("/medicalhub")
                .withAutomaticReconnect()
                .build();

            await this.signalRConnection.start();
            console.log('SignalR connected for Whisper transcription');
        } catch (error) {
            console.error('Error connecting to SignalR:', error);
        }
    }

    /**
     * Iniciar gravação de áudio
     */
    async startRecording(sessionId) {
        try {
            this.sessionId = sessionId;
            
            // Solicitar permissão do microfone com configurações otimizadas
            const stream = await navigator.mediaDevices.getUserMedia({
                audio: {
                    sampleRate: 16000,
                    channelCount: 1,
                    echoCancellation: true,
                    noiseSuppression: true,
                    autoGainControl: true
                }
            });

            // Configurar MediaRecorder para capturar em chunks
            this.mediaRecorder = new MediaRecorder(stream, {
                mimeType: 'audio/webm;codecs=opus'
            });

            this.audioChunks = [];
            
            // Configurar eventos do MediaRecorder
            this.mediaRecorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    this.audioChunks.push(event.data);
                    
                    // Se chunk atingiu tamanho adequado, processar
                    if (this.getCurrentAudioSize() >= this.config.chunkSize) {
                        this.processAudioChunk();
                    }
                }
            };

            this.mediaRecorder.onstop = () => {
                // Processar último chunk quando parar
                if (this.audioChunks.length > 0) {
                    this.processAudioChunk();
                }
            };

            // Iniciar gravação com chunks de 30 segundos
            this.mediaRecorder.start(30000);
            this.isRecording = true;
            
            this.emit('recording-started', { sessionId });
            
            // Notificar via SignalR
            if (this.signalRConnection) {
                await this.signalRConnection.invoke("StartTranscription", sessionId);
            }
            
        } catch (error) {
            console.error('Error starting recording:', error);
            this.emit('error', { error: error.message });
        }
    }

    /**
     * Parar gravação
     */
    async stopRecording() {
        if (this.mediaRecorder && this.isRecording) {
            this.mediaRecorder.stop();
            this.isRecording = false;
            
            // Parar todas as tracks do stream
            if (this.mediaRecorder.stream) {
                this.mediaRecorder.stream.getTracks().forEach(track => track.stop());
            }
            
            this.emit('recording-stopped');
            
            // Notificar via SignalR
            if (this.signalRConnection && this.sessionId) {
                await this.signalRConnection.invoke("StopTranscription", this.sessionId);
            }
        }
    }

    /**
     * Processar chunk de áudio coletado
     */
    async processAudioChunk() {
        if (this.audioChunks.length === 0) return;

        try {
            // Combinar chunks em um blob
            const audioBlob = new Blob(this.audioChunks, { type: 'audio/webm' });
            
            // Limpar chunks processados
            this.audioChunks = [];
            
            // Enviar para transcrição
            const transcription = await this.transcribeAudio(audioBlob);
            
            if (transcription && transcription.text) {
                await this.processTranscriptionResult(transcription);
            }
            
        } catch (error) {
            console.error('Error processing audio chunk:', error);
            this.emit('error', { error: error.message });
        }
    }

    /**
     * Transcrever áudio usando OpenAI Whisper
     */
    async transcribeAudio(audioBlob) {
        try {
            // Converter para formato suportado se necessário
            const audioFile = await this.convertAudioIfNeeded(audioBlob);
            
            // Preparar FormData para a API
            const formData = new FormData();
            formData.append('file', audioFile, 'audio.webm');
            formData.append('model', this.config.deploymentName);
            formData.append('language', this.config.language);
            formData.append('response_format', this.config.responseFormat);
            
            if (this.config.enableTimestamps) {
                formData.append('timestamp_granularities[]', 'word');
                formData.append('timestamp_granularities[]', 'segment');
            }

            // Fazer requisição para Whisper API
            const response = await fetch(`${this.config.endpoint}openai/deployments/${this.config.deploymentName}/audio/transcriptions?api-version=2024-06-01`, {
                method: 'POST',
                headers: {
                    'api-key': this.config.apiKey
                },
                body: formData
            });

            if (!response.ok) {
                throw new Error(`Whisper API error: ${response.status} ${response.statusText}`);
            }

            const result = await response.json();
            return result;
            
        } catch (error) {
            console.error('Error transcribing audio with Whisper:', error);
            throw error;
        }
    }

    /**
     * Processar resultado da transcrição
     */
    async processTranscriptionResult(transcription) {
        try {
            // Processar segments com timestamps e possível diarização
            if (transcription.segments) {
                for (const segment of transcription.segments) {
                    const chunk = {
                        id: this.generateChunkId(),
                        sessionId: this.sessionId,
                        text: segment.text.trim(),
                        speaker: this.identifySpeaker(segment),
                        confidence: segment.no_speech_prob ? (1 - segment.no_speech_prob) : 0.95,
                        timestamp: new Date().toISOString(),
                        startTime: segment.start,
                        endTime: segment.end,
                        sequenceNumber: segment.id || 0
                    };

                    // Emitir chunk processado
                    this.emit('chunk-transcribed', chunk);
                    
                    // Enviar via SignalR para processamento pelos agentes
                    if (this.signalRConnection && this.sessionId) {
                        await this.signalRConnection.invoke("ProcessTranscriptionChunk", this.sessionId, chunk);
                    }
                }
            } else {
                // Fallback para texto simples
                const chunk = {
                    id: this.generateChunkId(),
                    sessionId: this.sessionId,
                    text: transcription.text.trim(),
                    speaker: "Speaker1",
                    confidence: 0.95,
                    timestamp: new Date().toISOString(),
                    sequenceNumber: Date.now()
                };

                this.emit('chunk-transcribed', chunk);
                
                if (this.signalRConnection && this.sessionId) {
                    await this.signalRConnection.invoke("ProcessTranscriptionChunk", this.sessionId, chunk);
                }
            }
            
        } catch (error) {
            console.error('Error processing transcription result:', error);
            this.emit('error', { error: error.message });
        }
    }

    /**
     * Identificar speaker usando análise simples
     * TODO: Implementar diarização mais sofisticada
     */
    identifySpeaker(segment) {
        // Análise simples baseada em padrões
        const text = segment.text.toLowerCase();
        
        // Padrões típicos de médico
        const doctorPatterns = [
            /vou prescrever|vou receitar|recomendo|você deve|tome este/,
            /diagnóstico|exame|consulta|retorno|medicamento/,
            /como está se sentindo|me fale sobre|quando começou/
        ];
        
        // Padrões típicos de paciente
        const patientPatterns = [
            /estou sentindo|me dói|tenho dor|não estou bem/,
            /doutor|doutora|posso|preciso|quando/,
            /ontem|hoje|semana passada|mês passado/
        ];
        
        for (const pattern of doctorPatterns) {
            if (pattern.test(text)) return "Médico";
        }
        
        for (const pattern of patientPatterns) {
            if (pattern.test(text)) return "Paciente";
        }
        
        return "Speaker1"; // Default
    }

    /**
     * Converter áudio para formato suportado se necessário
     */
    async convertAudioIfNeeded(audioBlob) {
        // Whisper suporta vários formatos, então retornamos o blob original
        // Se necessário, implementar conversão aqui
        return audioBlob;
    }

    /**
     * Obter tamanho atual do áudio acumulado
     */
    getCurrentAudioSize() {
        return this.audioChunks.reduce((total, chunk) => total + chunk.size, 0);
    }

    /**
     * Gerar ID único para chunk
     */
    generateChunkId() {
        return `whisper_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }

    /**
     * Event emitter
     */
    on(event, listener) {
        if (!this.events.has(event)) {
            this.events.set(event, []);
        }
        this.events.get(event).push(listener);
    }

    emit(event, data) {
        const listeners = this.events.get(event) || [];
        listeners.forEach(listener => {
            try {
                listener(data);
            } catch (error) {
                console.error(`Error in event listener for ${event}:`, error);
            }
        });
    }

    /**
     * Cleanup
     */
    dispose() {
        if (this.isRecording) {
            this.stopRecording();
        }
        
        if (this.signalRConnection) {
            this.signalRConnection.stop();
        }
        
        this.events.clear();
    }
}

// Tornar disponível globalmente
window.WhisperTranscriptionService = WhisperTranscriptionService;

// Auto-inicialização
document.addEventListener('DOMContentLoaded', () => {
    window.whisperService = new WhisperTranscriptionService({
        endpoint: 'https://medicalscribe-openai-eastus2.openai.azure.com/',
        enableDiarization: true
    });
    
    console.log('Whisper Transcription Service initialized');
});