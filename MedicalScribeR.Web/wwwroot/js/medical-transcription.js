class MedicalTranscription {
    constructor() {
        this.connection = null;
        this.speechRecognizer = null;
        this.sessionId = null;
        this.isRecording = false;
        this.userId = 'demo-user';
        this.userName = 'Dr. João Silva';
    }

    init(options = {}) {
        this.userId = options.userId || 'demo-user';
        this.userName = options.userName || 'Dr. João Silva';
        
        this.initializeSignalR();
        // setupEventHandlers is no longer needed here as events are handled in HTML
        this.updateUI();
    }

    async initializeSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/medicalhub")
            .withAutomaticReconnect()
            .build();

        this.connection.on("TranscriptionUpdate", (data) => {
            this.handleTranscriptionUpdate(data);
        });

        this.connection.on("AgentActivated", (data) => {
            this.handleAgentActivated(data);
        });

        this.connection.on("DocumentGenerated", (data) => {
            this.handleDocumentGenerated(data);
        });

        // SessionStarted and SessionStopped are now handled directly in voither-index.html

        this.connection.onreconnecting(error => {
            console.warn(`Connection lost due to error "${error}". Reconnecting.`);
            // showNotification is now handled by voither-index.html
        });

        this.connection.onreconnected(connectionId => {
            console.log(`Connection reestablished. Connected with connectionId ${connectionId}.`);
            // showNotification is now handled by voither-index.html
        });

        try {
            await this.connection.start();
            console.log("SignalR connection established");
        } catch (error) {
            console.error("SignalR connection failed:", error);
            // showNotification is now handled by voither-index.html
        }
    }

    async initializeSpeechRecognition() {
        try {
            const response = await fetch('/api/speech-token');
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            const speechData = await response.json();
            
            const speechConfig = SpeechSDK.SpeechConfig.fromSubscription(speechData.key, speechData.region);
            speechConfig.speechRecognitionLanguage = 'pt-BR';
            speechConfig.setProperty(SpeechSDK.PropertyId.SpeechServiceConnection_SpeakerIdEnabled, "true");
            
            const audioConfig = SpeechSDK.AudioConfig.fromDefaultMicrophoneInput();
            this.speechRecognizer = new SpeechSDK.SpeechRecognizer(speechConfig, audioConfig);

            this.speechRecognizer.recognized = (s, e) => {
                if (e.result.reason === SpeechSDK.ResultReason.RecognizedSpeech && e.result.text) {
                    const speakerId = e.result.properties.getProperty(SpeechSDK.PropertyId.SpeechServiceConnection_SpeakerId, "unknown");
                    this.processTranscriptionChunk(e.result.text, speakerId);
                }
            };

            this.speechRecognizer.sessionStarted = () => {
                console.log("Speech recognition session started");
            };

            this.speechRecognizer.sessionStopped = () => {
                console.log("Speech recognition session stopped");
            };

        } catch (error) {
            console.error("Speech recognition initialization failed:", error);
            // showNotification is now handled by voither-index.html
        }
    }

    // setupEventHandlers is no longer needed here as events are handled in HTML

    async startRecording() {
        try {
            const patientName = document.getElementById('patientName')?.value || 'Paciente Anônimo';
            const consultationType = document.getElementById('consultationType')?.value || 'consulta-geral';
            
            this.sessionId = `SESS-${Date.now()}`;
            
            // UI updates are now handled by voither-index.html
            
            await this.initializeSpeechRecognition();
            
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                await this.connection.invoke("StartTranscription", this.sessionId, patientName, consultationType);
            }
            
            if (this.speechRecognizer) {
                this.speechRecognizer.startContinuousRecognitionAsync();
            } else {
                // showNotification is now handled by voither-index.html
                return;
            }
            
            this.isRecording = true;
            // showNotification is now handled by voither-index.html
            
        } catch (error) {
            console.error("Error starting recording:", error);
            // showNotification is now handled by voither-index.html
        }
    }

    async stopRecording() {
        try {
            this.isRecording = false;
            
            if (this.speechRecognizer) {
                this.speechRecognizer.stopContinuousRecognitionAsync();
            }
            
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                await this.connection.invoke("StopTranscription", this.sessionId);
            }
            
            // UI updates are now handled by voither-index.html
            // showNotification is now handled by voither-index.html
            
        } catch (error) {
            console.error("Error stopping recording:", error);
            // showNotification is now handled by voither-index.html
        }
    }

    async processTranscriptionChunk(text, speakerId = 'unknown') {
        const chunk = {
            text: text,
            speaker: speakerId,
            timestamp: new Date().toISOString(),
            confidence: 0.95
        };

        this.displayTranscriptionChunk(chunk);
        
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            try {
                await this.connection.invoke("ProcessTranscriptionChunk", this.sessionId, chunk);
            } catch (error) {
                console.error("Error processing chunk:", error);
            }
        }
    }

    displayTranscriptionChunk(chunk) {
        const container = document.getElementById('transcriptionOutput');
        if (!container) return;

        const placeholder = container.querySelector('.text-center.text-gray-500');
        if (placeholder) {
            placeholder.remove();
        }

        const chunkElement = document.createElement('div');
        chunkElement.className = 'mb-2 p-2 border-l-4 border-blue-500'; // Tailwind classes
        
        const timestamp = new Date(chunk.timestamp).toLocaleTimeString();
        const confidenceClass = chunk.confidence > 0.8 ? 'text-green-600' : 
                               chunk.confidence > 0.6 ? 'text-yellow-600' : 'text-red-600'; // Tailwind classes
        
        const contentDiv = document.createElement('div');
        contentDiv.className = 'flex justify-between items-start'; // Tailwind classes

        const textDiv = document.createElement('div');
        textDiv.className = 'flex-grow-1';

        const smallEl = document.createElement('small');
        smallEl.className = 'text-gray-500'; // Tailwind classes
        smallEl.textContent = `[${timestamp}] ${chunk.speaker}:`;

        const transcriptionTextDiv = document.createElement('div');
        transcriptionTextDiv.className = 'text-gray-700'; // Tailwind classes
        transcriptionTextDiv.textContent = chunk.text; // Using textContent for security

        textDiv.appendChild(smallEl);
        textDiv.appendChild(transcriptionTextDiv);

        const confidenceSmall = document.createElement('small');
        confidenceSmall.className = `font-semibold ${confidenceClass}`;
        confidenceSmall.textContent = `${Math.round(chunk.confidence * 100)}%`;

        contentDiv.appendChild(textDiv);
        contentDiv.appendChild(confidenceSmall);

        chunkElement.appendChild(contentDiv);
        
        container.appendChild(chunkElement);
        container.scrollTop = container.scrollHeight;
    }

    handleTranscriptionUpdate(data) {
        this.displayTranscriptionChunk(data);
    }

    handleAgentActivated(data) {
        const agentElement = document.querySelector(`[data-agent="${data.AgentName}"]`);
        if (agentElement) {
            agentElement.classList.add('active'); // Keep 'active' for custom CSS
            agentElement.classList.add('border-blue-500', 'bg-blue-50'); // Tailwind classes
            const badge = agentElement.querySelector('.badge');
            if (badge) {
                badge.classList.remove('bg-gray-200', 'text-gray-800');
                badge.classList.add('bg-blue-500', 'text-white');
                badge.textContent = 'Ativo';
            }
        }
        // showNotification is now handled by voither-index.html
    }

    handleDocumentGenerated(data) {
        this.addGeneratedDocument(data);
        // showNotification is now handled by voither-index.html
    }

    addGeneratedDocument(document) {
        const container = document.getElementById('generatedDocuments'); // Changed from generated-documents
        if (!container) return;

        const placeholder = container.querySelector('.text-center.text-gray-500');
        if (placeholder) {
            placeholder.remove();
        }

        const docElement = document.createElement('div');
        docElement.className = 'bg-white p-4 rounded-lg border border-gray-200 shadow-sm'; // Tailwind classes
        docElement.innerHTML = `
            <div class="flex justify-between items-center mb-2">
                <h6 class="text-md font-semibold text-gray-800">
                    <svg class="w-5 h-5 inline-block mr-2 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"></path></svg>
                    ${document.type}
                </h6>
                <div class="flex space-x-2">
                    <button class="text-gray-500 hover:text-blue-600" onclick="downloadDocument('${document.documentId}')">
                        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"></path></svg>
                    </button>
                </div>
            </div>
            <p class="text-gray-700 text-sm mb-2">${document.content.substring(0, 150)}...</p>
            <small class="text-gray-500">
                Gerado por: ${document.generatedBy} | 
                Confiança: ${Math.round(document.confidence * 100)}%
            </small>
        `;
        
        container.appendChild(docElement);
    }

    updateUI() {
        // This function is now handled by the main script in voither-index.html
    }
}

// The following functions are now handled directly in voither-index.html or are no longer needed in this file:
// viewDocument, downloadDocument, updateRecordingUI, showStatus, showNotification, clearTranscription, saveTranscription, exportTranscription, startDurationTimer, stopDurationTimer, sendBotMessage, displayBotMessage

// The MedicalTranscription instance is now initialized in voither-index.html
