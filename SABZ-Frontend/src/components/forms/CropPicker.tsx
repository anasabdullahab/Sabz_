import { useEffect, useRef, useState } from 'react';
import { t } from '@/lib/i18n';
import { Search, Sprout, ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { CropKnowledgeEntryDto } from '@/types';

interface CropPickerProps {
  value: string;
  entries: CropKnowledgeEntryDto[];
  onChange: (name: string, entry: CropKnowledgeEntryDto | null) => void;
  error?: string;
}

/**
 * Searchable crop dropdown for the crop form.
 * Lists knowledge-base crops (with Urdu names and season badges) and falls
 * back to free text for custom crops — typing a name not in the list simply
 * keeps it as the crop name.
 */
export function CropPicker({ value, entries, onChange, error }: CropPickerProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState(value);
  const rootRef = useRef<HTMLDivElement>(null);

  // Stay in sync when the value changes externally (edit form initialization)
  useEffect(() => { setQuery(value); }, [value]);

  // Close on outside click
  useEffect(() => {
    const onDocMouseDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onDocMouseDown);
    return () => document.removeEventListener('mousedown', onDocMouseDown);
  }, []);

  const selectedEntry = entries.find((e) => e.name.toLowerCase() === value.trim().toLowerCase()) ?? null;

  const filtered = query.trim()
    ? entries.filter((e) => {
        const q = query.trim().toLowerCase();
        return (
          e.name.toLowerCase().includes(q) ||
          (e.nameUrdu && e.nameUrdu.includes(query.trim())) ||
          e.category.toLowerCase().includes(q)
        );
      })
    : entries;

  const handleInputChange = (text: string) => {
    setQuery(text);
    setOpen(true);
    const match = entries.find((e) => e.name.toLowerCase() === text.trim().toLowerCase()) ?? null;
    onChange(text, match);
  };

  const handleSelect = (entry: CropKnowledgeEntryDto) => {
    setQuery(entry.name);
    onChange(entry.name, entry);
    setOpen(false);
  };

  return (
    <div ref={rootRef} className="relative">
      <label className="text-xs font-medium text-gray-700 mb-1 block">{t('crop.name')} *</label>
      <div
        className={cn(
          'relative flex items-center',
          error && 'ring-2 ring-red-200 rounded-lg',
        )}
      >
        <Search className="absolute left-3 h-4 w-4 text-gray-400 pointer-events-none z-10" />
        <input
          type="text"
          value={query}
          onChange={(e) => handleInputChange(e.target.value)}
          onFocus={() => setOpen(true)}
          placeholder={t('crop.pickerPlaceholder')}
          maxLength={100}
          className={cn(
            'w-full pl-9 pr-9 py-2 rounded-lg border text-sm bg-white focus:outline-none focus:ring-2 focus:ring-primary-500',
            error ? 'border-red-300' : 'border-gray-200',
          )}
        />
        <button
          type="button"
          tabIndex={-1}
          onClick={() => setOpen((o) => !o)}
          className="absolute right-2 p-1 text-gray-400 hover:text-gray-600"
          aria-label="Toggle crop list"
        >
          <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} />
        </button>
      </div>

      {open && (
        <div className="absolute z-20 mt-1 w-full rounded-xl border border-gray-200 bg-white shadow-lg overflow-hidden max-h-64 overflow-y-auto">
          {filtered.length === 0 && (
            <div className="px-3 py-4 text-xs text-gray-500 flex items-center gap-2">
              <Sprout className="h-4 w-4 text-gray-300" />
              {query.trim() ? t('crop.customCropHint') : t('crop.noSuggestions')}
            </div>
          )}
          {filtered.map((entry) => (
            <button
              key={entry.name}
              type="button"
              onClick={() => handleSelect(entry)}
              className={cn(
                'w-full flex items-center justify-between gap-2 px-3 py-2.5 text-left hover:bg-primary-50 transition-colors',
                selectedEntry?.name === entry.name && 'bg-primary-50',
              )}
            >
              <div className="min-w-0">
                <p className="text-sm font-medium text-gray-900 truncate">
                  {entry.name}
                  <span className="text-gray-400 font-normal ml-1.5 text-xs">{entry.nameUrdu}</span>
                </p>
                <p className="text-[10px] text-gray-400">
                  {entry.category} · {entry.maturityDays} {t('crop.days')} · {entry.season}
                </p>
              </div>
              {selectedEntry?.name === entry.name && (
                <span className="text-[10px] font-semibold text-primary-700 shrink-0">✓</span>
              )}
            </button>
          ))}
        </div>
      )}
      {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
    </div>
  );
}
