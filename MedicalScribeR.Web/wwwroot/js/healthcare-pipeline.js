/**
 * ===================================================================
 * VOITHER MEDICAL SCRIBE - AZURE SERVICES INTEGRATION PIPELINE
 * Configuration for Healthcare AI Services Pipeline
 * ===================================================================
 */

class VoitherHealthcareServices {
    constructor() {
        this.services = {
            healthInsights: {
                endpoint: 'https://insightshealth.cognitiveservices.azure.com/',
                apiVersion: '2024-04-01',
                capabilities: ['clinical-reasoning', 'radiology-insights', 'trial-matching']
            },
            textAnalytics: {
                primary: 'https://healthcaranlp.cognitiveservices.azure.com/',
                backup: 'https://etherim.cognitiveservices.azure.com/',
                capabilities: ['entity-extraction', 'sentiment-analysis', 'umls-linking', 'fhir-output']
            },
            cognitiveSearch: {
                endpoint: 'https://healthcaranlp-asuxj7c6w3imp6w.search.windows.net',
                capabilities: ['semantic-search', 'knowledge-base', 'medical-indexing']
            },
            healthcareApis: {
                workspace: 'medicalhub',
                region: 'eastus2',
                capabilities: ['fhir-r4', 'clinical-documents', 'patient-records']
            },
            healthBot: {
                resourceId: '/subscriptions/2290fbe4-e0ae-46e4-9bdd-dd5f7b5397d5/resourceGroups/rg-medicalscribe/providers/Microsoft.HealthBot/healthBots/voitbot',
                capabilities: ['conversational-ai', 'medical-qa', 'triage-support']
            }
        };
        
        this.pipeline = null;
        this.initialized = false;
    }

    /**
     * Initialize the healthcare services pipeline
     */
    async initialize() {
        try {
            console.log('🏥 Initializing Voither Healthcare Services Pipeline...');
            
            // Initialize services in order
            await this.initializeHealthInsights();
            await this.initializeTextAnalytics();
            await this.initializeCognitiveSearch();
            await this.initializeHealthcareApis();
            await this.initializeHealthBot();
            
            this.pipeline = new HealthcarePipeline(this.services);
            this.initialized = true;
            
            console.log('✅ Healthcare Services Pipeline initialized successfully');
            this.showNotification('Healthcare AI Services Pipeline Ready!', 'success');
            
            return true;
        } catch (error) {
            console.error('❌ Failed to initialize healthcare services:', error);
            this.showNotification('Failed to initialize healthcare services', 'error');
            return false;
        }
    }

    /**
     * Initialize Health Insights service
     */
    async initializeHealthInsights() {
        try {
            console.log('🧠 Initializing Health Insights...');
            
            // Test connection
            const response = await this.testServiceConnection(
                `${this.services.healthInsights.endpoint}health-insights/clinical-reasoning/jobs`,
                { 'Ocp-Apim-Subscription-Key': await this.getApiKey('health-insights') }
            );
            
            if (response.ok) {
                console.log('✅ Health Insights service ready');
                return true;
            }
            throw new Error('Health Insights connection failed');
        } catch (error) {
            console.error('❌ Health Insights initialization failed:', error);
            throw error;
        }
    }

    /**
     * Initialize Text Analytics service
     */
    async initializeTextAnalytics() {
        try {
            console.log('📝 Initializing Text Analytics for Healthcare...');
            
            // Test primary endpoint
            const primaryResponse = await this.testServiceConnection(
                `${this.services.textAnalytics.primary}text/analytics/v3.1/entities/health/jobs`,
                { 'Ocp-Apim-Subscription-Key': await this.getApiKey('text-analytics-primary') }
            );
            
            if (primaryResponse.ok) {
                console.log('✅ Text Analytics (Primary) service ready');
                return true;
            }
            
            // Fallback to backup endpoint
            console.log('🔄 Trying backup Text Analytics endpoint...');
            const backupResponse = await this.testServiceConnection(
                `${this.services.textAnalytics.backup}text/analytics/v3.1/entities/health/jobs`,
                { 'Ocp-Apim-Subscription-Key': await this.getApiKey('text-analytics-backup') }
            );
            
            if (backupResponse.ok) {
                console.log('✅ Text Analytics (Backup) service ready');
                return true;
            }
            
            throw new Error('Both Text Analytics endpoints failed');
        } catch (error) {
            console.error('❌ Text Analytics initialization failed:', error);
            throw error;
        }
    }

    /**
     * Initialize Cognitive Search service
     */
    async initializeCognitiveSearch() {
        try {
            console.log('🔍 Initializing Cognitive Search...');
            
            const response = await this.testServiceConnection(
                `${this.services.cognitiveSearch.endpoint}/indexes`,
                { 'api-key': await this.getApiKey('cognitive-search') }
            );
            
            if (response.ok) {
                console.log('✅ Cognitive Search service ready');
                return true;
            }
            throw new Error('Cognitive Search connection failed');
        } catch (error) {
            console.error('❌ Cognitive Search initialization failed:', error);
            throw error;
        }
    }

    /**
     * Initialize Healthcare APIs (FHIR)
     */
    async initializeHealthcareApis() {
        try {
            console.log('🏥 Initializing Healthcare APIs (FHIR)...');
            
            const fhirEndpoint = `https://${this.services.healthcareApis.workspace}-azurehealthcareapis-fhir.fhir.azurehealthcareapis.com`;
            const response = await this.testServiceConnection(
                `${fhirEndpoint}/metadata`,
                { 'Authorization': `Bearer ${await this.getAccessToken()}` }
            );
            
            if (response.ok) {
                console.log('✅ Healthcare APIs (FHIR) service ready');
                return true;
            }
            throw new Error('Healthcare APIs connection failed');
        } catch (error) {
            console.error('❌ Healthcare APIs initialization failed:', error);
            throw error;
        }
    }

    /**
     * Initialize Health Bot service
     */
    async initializeHealthBot() {
        try {
            console.log('🤖 Initializing Health Bot...');
            
            // Health Bot typically uses WebChat or Direct Line for integration
            // For now, we'll mark it as ready since it's a UI component
            console.log('✅ Health Bot integration ready');
            return true;
        } catch (error) {
            console.error('❌ Health Bot initialization failed:', error);
            throw error;
        }
    }

    /**
     * Process medical transcription through the pipeline
     */
    async processTranscription(transcriptionText, patientContext = {}) {
        if (!this.initialized) {
            throw new Error('Healthcare services not initialized');
        }
        
        try {
            console.log('🔄 Processing transcription through healthcare pipeline...');
            
            // Step 1: Extract entities and sentiment
            const entitiesResult = await this.extractHealthcareEntities(transcriptionText);
            
            // Step 2: Get clinical insights
            const clinicalInsights = await this.getClinicalInsights(transcriptionText, patientContext);
            
            // Step 3: Search knowledge base
            const knowledgeResults = await this.searchMedicalKnowledge(entitiesResult.entities);
            
            // Step 4: Create FHIR resources
            const fhirResources = await this.createFHIRResources(entitiesResult, clinicalInsights, patientContext);
            
            // Step 5: Generate comprehensive report
            const report = {
                transcription: transcriptionText,
                entities: entitiesResult.entities,
                sentiment: entitiesResult.sentiment,
                clinicalInsights: clinicalInsights,
                knowledgeBase: knowledgeResults,
                fhirResources: fhirResources,
                processedAt: new Date().toISOString(),
                confidence: this.calculateOverallConfidence(entitiesResult, clinicalInsights)
            };
            
            console.log('✅ Transcription processed successfully');
            return report;
            
        } catch (error) {
            console.error('❌ Error processing transcription:', error);
            throw error;
        }
    }

    /**
     * Extract healthcare entities and sentiment
     */
    async extractHealthcareEntities(text) {
        try {
            const endpoint = `${this.services.textAnalytics.primary}text/analytics/v3.1/entities/health/jobs`;
            
            const response = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Ocp-Apim-Subscription-Key': await this.getApiKey('text-analytics-primary')
                },
                body: JSON.stringify({
                    documents: [{
                        id: '1',
                        language: 'pt',
                        text: text
                    }]
                })
            });
            
            if (!response.ok) {
                throw new Error(`Text Analytics API error: ${response.status}`);
            }
            
            const jobUrl = response.headers.get('operation-location');
            return await this.pollJobResult(jobUrl);
            
        } catch (error) {
            console.error('Error extracting healthcare entities:', error);
            throw error;
        }
    }

    /**
     * Get clinical insights from Health Insights
     */
    async getClinicalInsights(text, patientContext) {
        try {
            const endpoint = `${this.services.healthInsights.endpoint}health-insights/clinical-reasoning/jobs`;
            
            const response = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Ocp-Apim-Subscription-Key': await this.getApiKey('health-insights')
                },
                body: JSON.stringify({
                    patients: [{
                        id: patientContext.id || 'patient-1',
                        sex: patientContext.sex || 'unknown',
                        birthDate: patientContext.birthDate || '1980-01-01',
                        encounters: [{
                            id: 'encounter-1',
                            period: {
                                start: new Date().toISOString(),
                                end: new Date().toISOString()
                            },
                            class: 'inpatient'
                        }]
                    }],
                    configuration: {
                        includeEvidence: true,
                        inferenceOptions: {
                            followupRecommendations: {
                                includeRecommendations: true,
                                includeEvidence: true
                            }
                        }
                    }
                })
            });
            
            if (!response.ok) {
                throw new Error(`Health Insights API error: ${response.status}`);
            }
            
            const jobUrl = response.headers.get('operation-location');
            return await this.pollJobResult(jobUrl);
            
        } catch (error) {
            console.error('Error getting clinical insights:', error);
            throw error;
        }
    }

    /**
     * Search medical knowledge base
     */
    async searchMedicalKnowledge(entities) {
        try {
            const searchTerms = entities
                .filter(entity => entity.category === 'Diagnosis' || entity.category === 'Symptom')
                .map(entity => entity.text)
                .join(' OR ');
            
            if (!searchTerms) return [];
            
            const endpoint = `${this.services.cognitiveSearch.endpoint}/indexes/medical-knowledge/docs/search`;
            
            const response = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'api-key': await this.getApiKey('cognitive-search')
                },
                body: JSON.stringify({
                    search: searchTerms,
                    searchMode: 'any',
                    top: 10,
                    select: 'title,content,category,confidence'
                })
            });
            
            if (!response.ok) {
                throw new Error(`Cognitive Search API error: ${response.status}`);
            }
            
            const result = await response.json();
            return result.value;
            
        } catch (error) {
            console.error('Error searching medical knowledge:', error);
            return [];
        }
    }

    /**
     * Create FHIR resources
     */
    async createFHIRResources(entitiesResult, clinicalInsights, patientContext) {
        try {
            const fhirEndpoint = `https://${this.services.healthcareApis.workspace}-azurehealthcareapis-fhir.fhir.azurehealthcareapis.com`;
            
            // Create Patient resource if not exists
            const patient = await this.createOrUpdatePatient(fhirEndpoint, patientContext);
            
            // Create Observation resources from entities
            const observations = await Promise.all(
                entitiesResult.entities
                    .filter(entity => entity.category === 'Symptom' || entity.category === 'Diagnosis')
                    .map(entity => this.createObservation(fhirEndpoint, patient.id, entity))
            );
            
            // Create DocumentReference for the transcription
            const documentRef = await this.createDocumentReference(fhirEndpoint, patient.id, entitiesResult.transcription);
            
            return {
                patient,
                observations,
                documentReference: documentRef
            };
            
        } catch (error) {
            console.error('Error creating FHIR resources:', error);
            throw error;
        }
    }

    /**
     * Calculate overall confidence score
     */
    calculateOverallConfidence(entitiesResult, clinicalInsights) {
        const entityConfidences = entitiesResult.entities.map(e => e.confidence || 0.5);
        const avgEntityConfidence = entityConfidences.length > 0 
            ? entityConfidences.reduce((sum, conf) => sum + conf, 0) / entityConfidences.length 
            : 0.5;
        
        // Simple confidence calculation - can be enhanced
        return Math.round(avgEntityConfidence * 100);
    }

    /**
     * Utility methods
     */
    async testServiceConnection(url, headers) {
        try {
            return await fetch(url, { method: 'GET', headers });
        } catch (error) {
            return { ok: false, error };
        }
    }

    async getApiKey(service) {
        // In production, these would come from Azure Key Vault
        const keyMap = {
            'health-insights': process.env.HEALTH_INSIGHTS_KEY || 'YOUR_HEALTH_INSIGHTS_KEY',
            'text-analytics-primary': process.env.TEXT_ANALYTICS_PRIMARY_KEY || 'YOUR_TEXT_ANALYTICS_PRIMARY_KEY',
            'text-analytics-backup': process.env.TEXT_ANALYTICS_BACKUP_KEY || 'YOUR_TEXT_ANALYTICS_BACKUP_KEY',
            'cognitive-search': process.env.COGNITIVE_SEARCH_KEY || 'YOUR_COGNITIVE_SEARCH_KEY'
        };
        
        return keyMap[service] || '';
    }

    async getAccessToken() {
        // In production, this would use managed identity or service principal
        return process.env.FHIR_ACCESS_TOKEN || 'YOUR_FHIR_ACCESS_TOKEN';
    }

    async pollJobResult(jobUrl, maxAttempts = 30) {
        for (let attempt = 0; attempt < maxAttempts; attempt++) {
            try {
                const response = await fetch(jobUrl, {
                    headers: {
                        'Ocp-Apim-Subscription-Key': await this.getApiKey('text-analytics-primary')
                    }
                });
                
                const result = await response.json();
                
                if (result.status === 'succeeded') {
                    return result.results;
                } else if (result.status === 'failed') {
                    throw new Error('Job failed: ' + JSON.stringify(result.errors));
                }
                
                // Wait before next poll
                await new Promise(resolve => setTimeout(resolve, 2000));
                
            } catch (error) {
                console.error('Error polling job result:', error);
                throw error;
            }
        }
        
        throw new Error('Job polling timeout');
    }

    showNotification(message, type) {
        if (window.VoitherUI) {
            window.VoitherUI.showNotification(message, type);
        } else {
            console.log(`${type.toUpperCase()}: ${message}`);
        }
    }
}

/**
 * Healthcare Pipeline orchestrator
 */
class HealthcarePipeline {
    constructor(services) {
        this.services = services;
        this.processingQueue = [];
        this.isProcessing = false;
    }

    async processTranscription(text, patientContext) {
        return new Promise((resolve, reject) => {
            this.processingQueue.push({
                text,
                patientContext,
                resolve,
                reject,
                timestamp: Date.now()
            });
            
            this.processQueue();
        });
    }

    async processQueue() {
        if (this.isProcessing || this.processingQueue.length === 0) {
            return;
        }
        
        this.isProcessing = true;
        
        while (this.processingQueue.length > 0) {
            const job = this.processingQueue.shift();
            
            try {
                const result = await this.executeProcessing(job.text, job.patientContext);
                job.resolve(result);
            } catch (error) {
                job.reject(error);
            }
        }
        
        this.isProcessing = false;
    }

    async executeProcessing(text, patientContext) {
        // Implementation would call the various Azure services
        // This is a placeholder for the actual processing logic
        console.log('Processing transcription through healthcare pipeline...');
        
        return {
            processed: true,
            timestamp: new Date().toISOString(),
            text,
            patientContext
        };
    }
}

// Global instance
window.VoitherHealthcare = new VoitherHealthcareServices();

// Initialize when document is ready
document.addEventListener('DOMContentLoaded', async () => {
    console.log('🏥 Voither Healthcare Services loading...');
    
    // Initialize with a small delay to ensure other services are ready
    setTimeout(async () => {
        try {
            await window.VoitherHealthcare.initialize();
        } catch (error) {
            console.error('Failed to initialize healthcare services:', error);
        }
    }, 1000);
});

// Export for Node.js environments
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { VoitherHealthcareServices, HealthcarePipeline };
}
