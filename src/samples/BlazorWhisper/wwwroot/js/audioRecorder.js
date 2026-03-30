// Audio Recorder - JavaScript interop for Blazor Whisper
// Uses Web Audio API to capture 16kHz mono PCM and encode as WAV

window.audioRecorder = {
    _stream: null,
    _audioContext: null,
    _sourceNode: null,
    _processorNode: null,
    _dotNetHelper: null,
    _samples: [],
    _isRecording: false,
    _realtimeInterval: null,

    initialize: async function () {
        if (this._stream) return;

        this._stream = await navigator.mediaDevices.getUserMedia({
            audio: {
                channelCount: 1,
                sampleRate: 16000,
                echoCancellation: true,
                noiseSuppression: true
            }
        });
    },

    startRecording: async function (dotNetHelper) {
        this._dotNetHelper = dotNetHelper;
        this._samples = [];
        this._isRecording = true;

        this._audioContext = new AudioContext({ sampleRate: 16000 });
        this._sourceNode = this._audioContext.createMediaStreamSource(this._stream);

        // ScriptProcessorNode for broad browser compatibility
        this._processorNode = this._audioContext.createScriptProcessor(4096, 1, 1);
        this._processorNode.onaudioprocess = (e) => {
            if (!this._isRecording) return;
            const channelData = e.inputBuffer.getChannelData(0);
            this._samples.push(new Float32Array(channelData));
        };

        this._sourceNode.connect(this._processorNode);
        this._processorNode.connect(this._audioContext.destination);
    },

    stopRecording: async function () {
        this._isRecording = false;

        if (this._processorNode) {
            this._processorNode.disconnect();
            this._processorNode = null;
        }
        if (this._sourceNode) {
            this._sourceNode.disconnect();
            this._sourceNode = null;
        }
        if (this._audioContext) {
            await this._audioContext.close();
            this._audioContext = null;
        }

        const wavBytes = this._encodeWav(this._mergeSamples(this._samples), 16000);
        this._samples = [];

        if (this._dotNetHelper) {
            try {
                this._dotNetHelper.invokeMethodAsync('OnRecordingComplete', Array.from(wavBytes));
            } catch (e) {
                console.error('Error sending recording to .NET:', e);
                this._dotNetHelper.invokeMethodAsync('OnRecordingError', e.message || 'Unknown error');
            }
        }
    },

    startRealtimeRecording: async function (dotNetHelper, chunkIntervalMs) {
        this._dotNetHelper = dotNetHelper;
        this._samples = [];
        this._isRecording = true;

        this._audioContext = new AudioContext({ sampleRate: 16000 });
        this._sourceNode = this._audioContext.createMediaStreamSource(this._stream);
        this._processorNode = this._audioContext.createScriptProcessor(4096, 1, 1);

        this._processorNode.onaudioprocess = (e) => {
            if (!this._isRecording) return;
            const channelData = e.inputBuffer.getChannelData(0);
            this._samples.push(new Float32Array(channelData));
        };

        this._sourceNode.connect(this._processorNode);
        this._processorNode.connect(this._audioContext.destination);

        // Send chunks at the configured interval
        this._realtimeInterval = setInterval(async () => {
            if (!this._isRecording || this._samples.length === 0) return;

            const currentSamples = this._samples.splice(0);
            const wavBytes = this._encodeWav(this._mergeSamples(currentSamples), 16000);

            try {
                await dotNetHelper.invokeMethodAsync('OnAudioChunkReady', Array.from(wavBytes));
            } catch (e) {
                console.error('Error sending chunk to .NET:', e);
                dotNetHelper.invokeMethodAsync('OnRealtimeError', e.message || 'Chunk send error');
            }
        }, chunkIntervalMs);
    },

    stopRealtimeRecording: async function () {
        this._isRecording = false;

        if (this._realtimeInterval) {
            clearInterval(this._realtimeInterval);
            this._realtimeInterval = null;
        }

        // Flush remaining samples as a final chunk
        if (this._samples.length > 0 && this._dotNetHelper) {
            const currentSamples = this._samples.splice(0);
            const wavBytes = this._encodeWav(this._mergeSamples(currentSamples), 16000);
            try {
                await this._dotNetHelper.invokeMethodAsync('OnAudioChunkReady', Array.from(wavBytes));
            } catch (e) {
                console.error('Error sending final chunk:', e);
            }
        }

        if (this._processorNode) {
            this._processorNode.disconnect();
            this._processorNode = null;
        }
        if (this._sourceNode) {
            this._sourceNode.disconnect();
            this._sourceNode = null;
        }
        if (this._audioContext) {
            await this._audioContext.close();
            this._audioContext = null;
        }

        this._samples = [];
    },

    _mergeSamples: function (sampleArrays) {
        let totalLength = 0;
        for (const arr of sampleArrays) {
            totalLength += arr.length;
        }
        const merged = new Float32Array(totalLength);
        let offset = 0;
        for (const arr of sampleArrays) {
            merged.set(arr, offset);
            offset += arr.length;
        }
        return merged;
    },

    _encodeWav: function (samples, sampleRate) {
        const numChannels = 1;
        const bitsPerSample = 16;
        const byteRate = sampleRate * numChannels * (bitsPerSample / 8);
        const blockAlign = numChannels * (bitsPerSample / 8);
        const dataSize = samples.length * (bitsPerSample / 8);
        const headerSize = 44;
        const buffer = new ArrayBuffer(headerSize + dataSize);
        const view = new DataView(buffer);

        // RIFF header
        this._writeString(view, 0, 'RIFF');
        view.setUint32(4, 36 + dataSize, true);
        this._writeString(view, 8, 'WAVE');

        // fmt sub-chunk
        this._writeString(view, 12, 'fmt ');
        view.setUint32(16, 16, true);          // sub-chunk size
        view.setUint16(20, 1, true);           // PCM format
        view.setUint16(22, numChannels, true);
        view.setUint32(24, sampleRate, true);
        view.setUint32(28, byteRate, true);
        view.setUint16(32, blockAlign, true);
        view.setUint16(34, bitsPerSample, true);

        // data sub-chunk
        this._writeString(view, 36, 'data');
        view.setUint32(40, dataSize, true);

        // Write PCM samples (float32 -> int16)
        let offset = 44;
        for (let i = 0; i < samples.length; i++) {
            let sample = Math.max(-1, Math.min(1, samples[i]));
            sample = sample < 0 ? sample * 0x8000 : sample * 0x7FFF;
            view.setInt16(offset, sample, true);
            offset += 2;
        }

        return new Uint8Array(buffer);
    },

    _writeString: function (view, offset, str) {
        for (let i = 0; i < str.length; i++) {
            view.setUint8(offset + i, str.charCodeAt(i));
        }
    }
};
