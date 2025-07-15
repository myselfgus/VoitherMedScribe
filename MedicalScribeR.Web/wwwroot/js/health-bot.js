/**
 * Azure Health Bot Integration
 * Integra o frontend com o Azure Health Bot via API
 */
class HealthBotIntegration {
    constructor() {
        this.conversationId = null;
        this.isConnected = false;
        this.apiBaseUrl = '/api/healthbot';
        this.messageHistory = [];
        this.initializeElements();
        this.setupEventListeners();
    }

    initializeElements() {
        this.chatWindow = document.getElementById('bot-chat-window');
        this.messageInput = document.getElementById('bot-input');
        this.sendButton = document.getElementById('bot-send-btn');
        this.suggestionsContainer = document.createElement('div');
        this.suggestionsContainer.className = 'suggestions-container mt-2';
        
        // Adicionar container de sugestões após o input
        if (this.messageInput && this.messageInput.parentNode) {
            this.messageInput.parentNode.appendChild(this.suggestionsContainer);
        }
    }

    setupEventListeners() {
        if (this.sendButton) {
            this.sendButton.addEventListener('click', () => this.sendMessage());
        }

        if (this.messageInput) {
            this.messageInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    this.sendMessage();
                }
            });

            this.messageInput.addEventListener('input', () => {
                this.clearSuggestions();
            });
        }
    }

    /**
     * Inicia uma nova conversa com o Health Bot
     */
    async startConversation() {
        try {
            this.showTypingIndicator();
            
            const response = await fetch(`${this.apiBaseUrl}/start-conversation`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                },
                body: JSON.stringify({
                    userId: this.getCurrentUserId(),
                    userName: this.getCurrentUserName()
                })
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();
            this.conversationId = data.conversationId;
            this.isConnected = true;

            this.hideTypingIndicator();
            this.displayMessage(data.welcomeMessage, 'bot');
            this.loadSuggestedQuestions();

            console.log('Conversa Health Bot iniciada:', this.conversationId);
            return data;
        } catch (error) {
            console.error('Erro ao iniciar conversa Health Bot:', error);
            this.hideTypingIndicator();
            this.displayMessage('Desculpe, não foi possível conectar com o assistente no momento. Tente novamente em alguns instantes.', 'bot', true);
            return null;
        }
    }

    /**
     * Envia mensagem para o Health Bot
     */
    async sendMessage() {
        const message = this.messageInput?.value.trim();
        if (!message) return;

        // Limpar input
        this.messageInput.value = '';
        this.clearSuggestions();

        // Verificar se há conversa ativa
        if (!this.conversationId) {
            await this.startConversation();
            if (!this.conversationId) return;
        }

        // Exibir mensagem do usuário
        this.displayMessage(message, 'user');
        this.showTypingIndicator();

        try {
            const payload = {
                conversationId: this.conversationId,
                message: message,
                medicalContext: this.getMedicalContext()
            };

            const response = await fetch(`${this.apiBaseUrl}/send-message`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();
            this.hideTypingIndicator();

            // Exibir respostas do bot
            if (data.messages && data.messages.length > 0) {
                data.messages.forEach(msg => {
                    if (msg.from === 'bot') {
                        this.displayMessage(msg.text, 'bot', false, msg.confidence);
                    }
                });
            } else {
                this.displayMessage('Recebi sua mensagem. Como posso ajudar mais?', 'bot');
            }

            // Carregar novas sugestões baseadas no contexto
            this.loadSuggestedQuestions(message);

        } catch (error) {
            console.error('Erro ao enviar mensagem:', error);
            this.hideTypingIndicator();
            this.displayMessage('Desculpe, houve um problema ao processar sua mensagem. Tente novamente.', 'bot', true);
        }
    }

    /**
     * Envia contexto médico para o bot
     */
    async sendMedicalContext(sessionId, transcriptionText, consultationType) {
        if (!this.conversationId) {
            await this.startConversation();
            if (!this.conversationId) return;
        }

        try {
            const payload = {
                conversationId: this.conversationId,
                sessionId: sessionId,
                transcriptionText: transcriptionText,
                consultationType: consultationType
            };

            const response = await fetch(`${this.apiBaseUrl}/send-medical-context`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                const data = await response.json();
                if (data.messages && data.messages.length > 0) {
                    data.messages.forEach(msg => {
                        if (msg.from === 'bot') {
                            this.displayMessage(msg.text, 'bot');
                        }
                    });
                }
                this.loadSuggestedQuestions(transcriptionText);
            }
        } catch (error) {
            console.error('Erro ao enviar contexto médico:', error);
        }
    }

    /**
     * Carrega sugestões de perguntas
     */
    async loadSuggestedQuestions(context = '') {
        if (!this.conversationId) return;

        try {
            const url = `${this.apiBaseUrl}/suggested-questions/${this.conversationId}?medicalContext=${encodeURIComponent(context)}`;
            const response = await fetch(url, {
                headers: {
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                }
            });

            if (response.ok) {
                const data = await response.json();
                this.displaySuggestions(data.questions);
            }
        } catch (error) {
            console.error('Erro ao carregar sugestões:', error);
        }
    }

    /**
     * Exibe mensagem no chat
     */
    displayMessage(text, sender, isError = false, confidence = null) {
        if (!this.chatWindow) return;

        const messageElement = document.createElement('div');
        messageElement.className = `message ${sender === 'user' ? 'user-message' : 'bot-message'} mb-2`;

        if (isError) {
            messageElement.classList.add('error-message');
        }

        const messageContent = document.createElement('div');
        messageContent.className = `message-content p-3 rounded-lg ${
            sender === 'user' 
                ? 'bg-blue-500 text-white ml-auto max-w-xs' 
                : isError 
                    ? 'bg-red-100 text-red-800 mr-auto max-w-md'
                    : 'bg-gray-100 text-gray-800 mr-auto max-w-md'
        }`;

        messageContent.innerHTML = this.formatMessage(text);

        // Adicionar indicador de confiança para mensagens do bot
        if (sender === 'bot' && confidence !== null && confidence < 0.8) {
            const confidenceIndicator = document.createElement('small');
            confidenceIndicator.className = 'confidence-indicator text-xs opacity-75 mt-1 block';
            confidenceIndicator.textContent = `Confiança: ${Math.round(confidence * 100)}%`;
            messageContent.appendChild(confidenceIndicator);
        }

        messageElement.appendChild(messageContent);

        // Adicionar timestamp
        const timestamp = document.createElement('small');
        timestamp.className = 'timestamp text-xs text-gray-500 mt-1 block';
        timestamp.textContent = new Date().toLocaleTimeString();
        messageElement.appendChild(timestamp);

        this.chatWindow.appendChild(messageElement);
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;

        // Salvar no histórico
        this.messageHistory.push({
            text: text,
            sender: sender,
            timestamp: new Date(),
            confidence: confidence
        });

        // Limitar histórico a 50 mensagens
        if (this.messageHistory.length > 50) {
            this.messageHistory = this.messageHistory.slice(-50);
        }
    }

    /**
     * Formata mensagem com markup básico
     */
    formatMessage(text) {
        return text
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
            .replace(/\*(.*?)\*/g, '<em>$1</em>')
            .replace(/\n/g, '<br>');
    }

    /**
     * Exibe sugestões de perguntas
     */
    displaySuggestions(questions) {
        if (!this.suggestionsContainer || !questions || questions.length === 0) return;

        this.clearSuggestions();

        const title = document.createElement('small');
        title.className = 'text-gray-600 font-medium block mb-2';
        title.textContent = 'Sugestões:';
        this.suggestionsContainer.appendChild(title);

        const suggestionsGrid = document.createElement('div');
        suggestionsGrid.className = 'suggestions-grid grid grid-cols-1 sm:grid-cols-2 gap-2';

        questions.slice(0, 4).forEach(question => {
            const suggestionButton = document.createElement('button');
            suggestionButton.className = 'suggestion-btn text-left p-2 text-xs bg-blue-50 hover:bg-blue-100 text-blue-700 rounded border border-blue-200 transition-colors';
            suggestionButton.textContent = question;
            suggestionButton.onclick = () => {
                this.messageInput.value = question;
                this.sendMessage();
            };
            suggestionsGrid.appendChild(suggestionButton);
        });

        this.suggestionsContainer.appendChild(suggestionsGrid);
    }

    /**
     * Limpa sugestões
     */
    clearSuggestions() {
        if (this.suggestionsContainer) {
            this.suggestionsContainer.innerHTML = '';
        }
    }

    /**
     * Mostra indicador de digitação
     */
    showTypingIndicator() {
        this.hideTypingIndicator(); // Remove existing indicator

        const typingElement = document.createElement('div');
        typingElement.id = 'typing-indicator';
        typingElement.className = 'typing-indicator mb-2';
        typingElement.innerHTML = `
            <div class="bg-gray-100 text-gray-600 p-3 rounded-lg mr-auto max-w-md">
                <span class="typing-dots">
                    <span></span><span></span><span></span>
                </span>
                Assistente está digitando...
            </div>
        `;

        if (this.chatWindow) {
            this.chatWindow.appendChild(typingElement);
            this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
        }
    }

    /**
     * Esconde indicador de digitação
     */
    hideTypingIndicator() {
        const indicator = document.getElementById('typing-indicator');
        if (indicator) {
            indicator.remove();
        }
    }

    /**
     * Obtém contexto médico atual
     */
    getMedicalContext() {
        // Integrar com dados da sessão atual
        const transcriptionText = document.getElementById('transcriptionDisplay')?.textContent || '';
        const consultationType = document.getElementById('consultationType')?.value || 'consulta-geral';
        const sessionId = document.getElementById('sessionId')?.value || '';

        return {
            patientInfo: {
                sessionId: sessionId
            },
            medicalHistory: transcriptionText.substring(0, 300),
            currentSymptoms: this.extractSymptoms(transcriptionText),
            consultationType: consultationType
        };
    }

    /**
     * Extrai sintomas do texto
     */
    extractSymptoms(text) {
        const symptoms = [];
        const symptomKeywords = ['dor', 'febre', 'tosse', 'náusea', 'tontura', 'cansaço'];
        
        symptomKeywords.forEach(keyword => {
            if (text.toLowerCase().includes(keyword)) {
                symptoms.push(keyword);
            }
        });

        return symptoms.join(', ') || 'Sem sintomas específicos';
    }

    /**
     * Obtém token de autenticação
     */
    async getAuthToken() {
        // Implementar obtenção de token se necessário
        return '';
    }

    /**
     * Obtém ID do usuário atual
     */
    getCurrentUserId() {
        return userInfo?.userId || 'anonymous';
    }

    /**
     * Obtém nome do usuário atual
     */
    getCurrentUserName() {
        return userInfo?.userName || 'Usuário';
    }

    /**
     * Encerra conversa
     */
    async endConversation() {
        if (!this.conversationId) return;

        try {
            await fetch(`${this.apiBaseUrl}/end-conversation/${this.conversationId}`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${await this.getAuthToken()}`
                }
            });

            this.conversationId = null;
            this.isConnected = false;
            this.messageHistory = [];
            
            console.log('Conversa Health Bot encerrada');
        } catch (error) {
            console.error('Erro ao encerrar conversa:', error);
        }
    }

    /**
     * Limpa chat
     */
    clearChat() {
        if (this.chatWindow) {
            this.chatWindow.innerHTML = `
                <div class="bot-message text-gray-700 mb-2">
                    <p>Chat limpo. Como posso ajudar?</p>
                </div>
            `;
        }
        this.clearSuggestions();
        this.messageHistory = [];
    }
}

// CSS para indicador de digitação
const typingIndicatorStyles = `
    .typing-dots {
        display: inline-block;
        position: relative;
        width: 30px;
        height: 10px;
    }

    .typing-dots span {
        position: absolute;
        width: 4px;
        height: 4px;
        background-color: #6b7280;
        border-radius: 50%;
        animation: typing 1.4s infinite;
    }

    .typing-dots span:nth-child(1) { left: 0; animation-delay: 0s; }
    .typing-dots span:nth-child(2) { left: 8px; animation-delay: 0.2s; }
    .typing-dots span:nth-child(3) { left: 16px; animation-delay: 0.4s; }

    @keyframes typing {
        0%, 60%, 100% { transform: translateY(0); opacity: 0.5; }
        30% { transform: translateY(-8px); opacity: 1; }
    }

    .suggestion-btn:hover {
        transform: translateY(-1px);
        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }

    .error-message {
        border-left: 3px solid #ef4444;
    }
`;

// Adicionar estilos ao documento
const styleSheet = document.createElement('style');
styleSheet.textContent = typingIndicatorStyles;
document.head.appendChild(styleSheet);

// Instância global do Health Bot
let healthBotIntegration = null;

// Inicializar quando o DOM estiver pronto
document.addEventListener('DOMContentLoaded', function() {
    healthBotIntegration = new HealthBotIntegration();
    
    // Auto-iniciar conversa quando usuário estiver autenticado
    if (typeof isAuthenticated !== 'undefined' && isAuthenticated) {
        setTimeout(() => {
            healthBotIntegration.startConversation();
        }, 2000);
    }
});

// Função global para integrar com transcription
function notifyHealthBotOfTranscription(sessionId, transcriptionText, consultationType) {
    if (healthBotIntegration) {
        healthBotIntegration.sendMedicalContext(sessionId, transcriptionText, consultationType);
    }
}
