import { useEffect, useState, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { agronomistApi } from '@/api/agronomistApi';
import { askAgronomist } from '@/lib/agronomistChat';
import { farmApi } from '@/api/farmApi';
import { parseApiError } from '@/api/client';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { ErrorState } from '@/components/ui/EmptyState';
import { PageSkeleton } from '@/components/ui/Skeleton';
import { t } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import {
  ArrowLeft, Send, Mic, MicOff, Bot, User, Sprout, Loader2,
  AlertTriangle, Cloud,
} from 'lucide-react';
import type { AgronomistResponseDto, VoiceAgronomistResponseDto, FarmResponseDto } from '@/types';

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  question: string;
  answer: string;
  transcription?: string;
  disclaimer?: string;
  error?: boolean;
}

export function AgronomistPage() {
  const { farmId } = useParams<{ farmId: string }>();
  const navigate = useNavigate();
  const [farm, setFarm] = useState<FarmResponseDto | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [recording, setRecording] = useState(false);
  const [uploading, setUploading] = useState(false);
  const mediaRecorder = useRef<MediaRecorder | null>(null);
  const audioChunks = useRef<Blob[]>([]);
  const chatEnd = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!farmId) return;
    farmApi.getById(farmId)
      .then(setFarm)
      .catch((err) => setError(parseApiError(err).message));
  }, [farmId]);

  useEffect(() => {
    chatEnd.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  if (error) return <ErrorState message={error} />;
  if (!farm) return <PageSkeleton />;

  const sendMessage = async (msg: string) => {
    if (!msg || !farmId || sending) return;
    setSending(true);

    const userMsg: ChatMessage = { id: crypto.randomUUID(), role: 'user', question: msg, answer: '' };
    setMessages((prev) => [...prev, userMsg]);

    // Prior exchanges give the Groq fallback conversation context.
    const history = messages
      .filter((m) => !m.error && m.answer)
      .map((m) => ({ question: m.question, answer: m.answer }));

    try {
      const result = await askAgronomist(farmId, msg, history);
      const assistantMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        question: result.question,
        answer: result.answer,
        disclaimer: result.disclaimer ?? undefined,
      };
      setMessages((prev) => [...prev, assistantMsg]);
    } catch {
      const errMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        question: msg,
        answer: t('agronomist.error'),
        error: true,
      };
      setMessages((prev) => [...prev, errMsg]);
    } finally {
      setSending(false);
    }
  };

  const handleSend = () => {
    const msg = input.trim();
    if (!msg) return;
    setInput('');
    sendMessage(msg);
  };

  const startRecording = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const recorder = new MediaRecorder(stream);
      audioChunks.current = [];
      recorder.ondataavailable = (e) => { if (e.data.size > 0) audioChunks.current.push(e.data); };
      recorder.onstop = async () => {
        stream.getTracks().forEach((track) => track.stop());
        const blob = new Blob(audioChunks.current, { type: 'audio/webm' });
        await handleVoiceUpload(blob);
      };
      recorder.start();
      mediaRecorder.current = recorder;
      setRecording(true);
    } catch {
      setMessages((prev) => [
        ...prev,
        { id: crypto.randomUUID(), role: 'assistant', question: '', answer: t('agronomist.voiceError'), error: true },
      ]);
    }
  };

  const stopRecording = () => {
    mediaRecorder.current?.stop();
    setRecording(false);
  };

  const handleVoiceUpload = async (blob: Blob) => {
    if (!farmId) return;
    setUploading(true);
    const file = new File([blob], 'voice-question.webm', { type: 'audio/webm' });

    const voiceMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      question: t('agronomist.transcribing'),
      answer: '',
    };
    setMessages((prev) => [...prev, voiceMsg]);

    try {
      const result: VoiceAgronomistResponseDto = await agronomistApi.voice(farmId, file);
      const assistantMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        question: result.question,
        answer: result.answer,
        transcription: result.transcription,
        disclaimer: result.disclaimer,
      };
      setMessages((prev) => {
        const updated = [...prev];
        const last = updated[updated.length - 1];
        if (last.id === voiceMsg.id) {
          updated[updated.length - 1] = { ...last, question: result.transcription };
        }
        return [...updated, assistantMsg];
      });
    } catch {
      setMessages((prev) => [
        ...prev.filter((m) => m.id !== voiceMsg.id),
        { id: crypto.randomUUID(), role: 'assistant', question: '', answer: t('agronomist.voiceError'), error: true },
      ]);
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="flex flex-col h-[calc(100vh-8rem)] animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between mb-4 shrink-0">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate(`/farms/${farmId}`)}
            className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center">
            <Bot className="h-5 w-5 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-gray-900">{t('agronomist.title')}</h1>
            <p className="text-xs text-gray-500">{farm.farmName}</p>
          </div>
        </div>
      </div>

      {/* Chat area */}
      <div className="flex-1 overflow-y-auto rounded-2xl bg-gray-50 border border-gray-100 p-4 space-y-4">
        {messages.length === 0 && (
          <div className="flex flex-col items-center justify-center h-full text-center">
            <Bot className="h-16 w-16 text-gray-300 mb-4" />
            <p className="text-sm text-gray-500">{t('agronomist.noMessages')}</p>
            {/* Quick-prompt chips */}
            <div className="flex flex-wrap justify-center gap-2 mt-4 max-w-md">
              {[
                t('agronomist.qFertilizer'),
                t('agronomist.qPest'),
                t('agronomist.qWater'),
              ].map((prompt) => (
                <button
                  key={prompt}
                  type="button"
                  onClick={() => sendMessage(prompt)}
                  disabled={sending || uploading}
                  className="px-3 py-1.5 rounded-full bg-white border border-gray-200 text-xs text-gray-600 hover:border-primary-300 hover:text-primary-700 hover:bg-primary-50 transition-colors disabled:opacity-50"
                >
                  {prompt}
                </button>
              ))}
            </div>
            <p className="text-xs text-gray-400 mt-4 max-w-xs">
              {t('agronomist.disclaimer')}
            </p>
          </div>
        )}

        {messages.map((msg) => (
          <div key={msg.id} className={cn('flex', msg.role === 'user' ? 'justify-end' : 'justify-start')}>
            <div className={cn('max-w-[85%] lg:max-w-[70%]', msg.role === 'user' ? 'order-1' : 'order-1')}>
              {msg.role === 'user' ? (
                <div className="bg-primary-600 text-white rounded-2xl rounded-br-md px-4 py-3">
                  <p className="text-sm whitespace-pre-wrap">{msg.question}</p>
                </div>
              ) : (
                <div className="space-y-2">
                  {msg.transcription && (
                    <div className="bg-white rounded-2xl rounded-bl-md px-4 py-3 border border-gray-100">
                      <p className="text-[10px] text-gray-400 font-medium mb-1">{t('agronomist.youAsked')}:</p>
                      <p className="text-sm text-gray-700 italic">"{msg.transcription}"</p>
                    </div>
                  )}
                  <div className={cn(
                    'rounded-2xl rounded-bl-md px-4 py-3 border',
                    msg.error ? 'bg-red-50 border-red-100' : 'bg-white border-gray-100',
                  )}>
                    <div className="flex items-center gap-2 mb-2">
                      <Bot className="h-3.5 w-3.5 text-indigo-500" />
                      <span className="text-[10px] font-semibold text-indigo-600 uppercase tracking-wider">
                        {t('agronomist.aiAnswer')}
                      </span>
                    </div>
                    <p className={cn('text-sm whitespace-pre-wrap', msg.error ? 'text-red-600' : 'text-gray-700')}>
                      {msg.answer}
                    </p>
                  </div>
                  {msg.disclaimer && (
                    <p className="text-[10px] text-gray-400 px-2">{msg.disclaimer}</p>
                  )}
                </div>
              )}
            </div>
          </div>
        ))}

        {(sending || uploading) && (
          <div className="flex justify-start">
            <div className="bg-white rounded-2xl rounded-bl-md px-4 py-3 border border-gray-100">
              <div className="flex items-center gap-2">
                <Loader2 className="h-4 w-4 animate-spin text-indigo-500" />
                <span className="text-sm text-gray-500">
                  {uploading ? t('agronomist.transcribing') : t('agronomist.sending')}
                </span>
              </div>
            </div>
          </div>
        )}
        <div ref={chatEnd} />
      </div>

      {/* Input area */}
      <div className="shrink-0 mt-3 flex items-center gap-2">
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); } }}
          placeholder={t('agronomist.askPlaceholder')}
          disabled={sending || uploading}
          className="flex-1 px-4 py-3 rounded-xl border border-gray-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent disabled:opacity-50"
        />
        {recording ? (
          <button
            onClick={stopRecording}
            className="h-11 w-11 rounded-xl bg-red-500 flex items-center justify-center text-white hover:bg-red-600 transition-colors shrink-0"
          >
            <MicOff className="h-5 w-5" />
          </button>
        ) : (
          <button
            onClick={startRecording}
            disabled={sending || uploading}
            className="h-11 w-11 rounded-xl bg-gray-100 flex items-center justify-center text-gray-500 hover:bg-gray-200 transition-colors disabled:opacity-50 shrink-0"
            title={t('agronomist.voice')}
          >
            <Mic className="h-5 w-5" />
          </button>
        )}
        <button
          onClick={handleSend}
          disabled={sending || uploading || !input.trim()}
          className="h-11 w-11 rounded-xl bg-primary-600 flex items-center justify-center text-white hover:bg-primary-700 transition-colors disabled:opacity-50 shrink-0"
        >
          <Send className="h-5 w-5" />
        </button>
      </div>

      <p className="text-[10px] text-gray-400 text-center mt-2 shrink-0">{t('agronomist.disclaimer')}</p>
    </div>
  );
}
