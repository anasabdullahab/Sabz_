/**
 * One-off dev utility: completes the Urdu dictionary in src/lib/i18n.ts.
 *
 * - Parses the EN + UR dictionaries from i18n.ts
 * - Sends every EN key missing from UR to OpenRouter (~google/gemini-flash-latest)
 *   in batches with a strict JSON-in/JSON-out localization prompt
 * - Merges translations (existing UR entries are kept) and rewrites the UR
 *   block in i18n.ts in EN key order
 *
 * Run: node scripts/translate-urdu.mjs   (from SABZ-Frontend/)
 */
import { readFileSync, writeFileSync } from 'node:fs';

const I18N_PATH = 'src/lib/i18n.ts';
const MODELS = ['~google/gemini-flash-latest', 'google/gemini-2.5-flash', 'openai/gpt-4o-mini'];
const BATCH_SIZE = 40;

const SYSTEM_PROMPT = `You are a professional English-to-Urdu localizer for "SABZ", a Pakistani farmer app.
You receive a JSON object mapping translation keys to English strings.
Translate EVERY value into natural, simple, respectful Urdu as used by Pakistani farmers.

Rules:
- Return ONLY a valid JSON object with the EXACT same keys; translate only the values.
- Keep placeholders like {count} exactly as-is.
- Keep emojis exactly as-is.
- Keep brand/product names untranslated: SABZ, Kisan Network, Disease Camera, AI, GPS, JWT, PKR.
- Farming vocabulary: Wheat=گندم, Rice=دھان, Cotton=کپاس, Potato=آلو, Tomato=ٹماٹر, fertilizer=کھاد, irrigation=آبپاشی, mandi=منڈی, acre=ایکڑ, crop=فصل, harvest=فصل کٹائی, pest=کیڑا, disease=بیماری.
- Keep translations concise (UI labels) and natural for a farming audience.
- Do not add quotes around values, do not add commentary, do not transliterate English words that have common Urdu equivalents.`;

function parseDict(block) {
  const dict = {};
  for (const line of block.split('\n')) {
    const m = line.match(/^\s*'([^']+)'\s*:\s*(.+?),?\s*$/);
    if (!m) continue;
    let v = m[2].trim();
    if (v.endsWith(',')) v = v.slice(0, -1).trim();
    if ((v.startsWith("'") && v.endsWith("'")) || (v.startsWith('"') && v.endsWith('"'))) {
      v = v.slice(1, -1);
      v = v.replace(/\\'/g, "'").replace(/\\"/g, '"');
    }
    if (v) dict[m[1]] = v;
  }
  return dict;
}

function extractJson(text) {
  let t = text.trim();
  const fence = t.match(/```(?:json)?\s*([\s\S]*?)```/);
  if (fence) t = fence[1].trim();
  const first = t.indexOf('{');
  const last = t.lastIndexOf('}');
  if (first === -1 || last === -1) throw new Error('no JSON object in response');
  return JSON.parse(t.slice(first, last + 1));
}

async function callOpenRouter(model, apiKey, payload) {
  const res = await fetch('https://openrouter.ai/api/v1/chat/completions', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      model,
      temperature: 0.2,
      messages: [
        { role: 'system', content: SYSTEM_PROMPT },
        { role: 'user', content: payload },
      ],
    }),
    signal: AbortSignal.timeout(120_000),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${(await res.text()).slice(0, 300)}`);
  const data = await res.json();
  const content = data?.choices?.[0]?.message?.content;
  if (!content) throw new Error('empty response');
  return content;
}

async function translateBatch(entries, apiKey) {
  const payload = JSON.stringify(Object.fromEntries(entries));
  for (const model of MODELS) {
    for (let attempt = 1; attempt <= 2; attempt++) {
      try {
        const content = await callOpenRouter(model, apiKey, payload);
        const parsed = extractJson(content);
        const result = {};
        for (const [key] of entries) {
          const value = parsed[key];
          if (typeof value === 'string' && value.trim()) result[key] = value.trim();
        }
        if (Object.keys(result).length === entries.length) {
          console.log(`  [${model}] ${entries.length} keys translated (attempt ${attempt})`);
          return result;
        }
        console.log(`  [${model}] attempt ${attempt}: ${entries.length - Object.keys(result).length} keys missing/invalid`);
      } catch (err) {
        console.log(`  [${model}] attempt ${attempt} failed: ${err.message}`);
      }
    }
  }
  return null;
}

function quoteValue(value) {
  if (value.includes("'")) {
    return `"${value.replace(/"/g, '\\"')}"`;
  }
  return `'${value}'`;
}

async function main() {
  const src = readFileSync(I18N_PATH, 'utf8');
  const enMatch = src.match(/const en: TranslationStrings = \{([\s\S]*?)\n\};/);
  const urMatch = src.match(/const ur: TranslationStrings = \{([\s\S]*?)\n\};/);
  if (!enMatch || !urMatch) throw new Error('could not locate dictionaries in i18n.ts');

  const en = parseDict(enMatch[1]);
  const ur = parseDict(urMatch[1]);
  const missing = Object.keys(en).filter((k) => !(k in ur));

  console.log(`EN keys: ${Object.keys(en).length}, existing UR keys: ${Object.keys(ur).length}, missing: ${missing.length}`);
  if (missing.length === 0) {
    console.log('Nothing to translate.');
    return;
  }

  const env = readFileSync('.env', 'utf8');
  const keyMatch = env.match(/^VITE_OPENROUTER_API_KEY=(.+)$/m);
  if (!keyMatch) throw new Error('VITE_OPENROUTER_API_KEY not found in .env');
  const apiKey = keyMatch[1].trim();

  const merged = { ...ur };
  for (let i = 0; i < missing.length; i += BATCH_SIZE) {
    const batch = missing.slice(i, i + BATCH_SIZE).map((k) => [k, en[k]]);
    console.log(`Batch ${Math.floor(i / BATCH_SIZE) + 1}/${Math.ceil(missing.length / BATCH_SIZE)} (${batch.length} keys)`);
    const translated = await translateBatch(batch, apiKey);
    if (translated) {
      Object.assign(merged, translated);
    } else {
      // Hard failure for this batch: fall back to the English text so the key
      // at least exists (i18n falls back to EN anyway, this keeps parity).
      for (const [k, v] of batch) merged[k] = v;
      console.log('  !! batch failed on all models — keeping English values');
    }
  }

  // Emit the merged UR dict in EN key order, grouped by EN section comments.
  const lines = [];
  for (const enLine of enMatch[1].split('\n')) {
    const comment = enLine.match(/^\s*(\/\/.*)$/);
    if (comment) {
      lines.push(`  ${comment[1]}`);
      continue;
    }
    const m = enLine.match(/^\s*'([^']+)'\s*:/);
    if (m && merged[m[1]] !== undefined) {
      lines.push(`  '${m[1]}': ${quoteValue(merged[m[1]])},`);
    }
  }

  const newUrBlock = `const ur: TranslationStrings = {\n${lines.join('\n')}\n};`;
  const updated = src.replace(/const ur: TranslationStrings = \{[\s\S]*?\n\};/, newUrBlock);
  writeFileSync(I18N_PATH, updated);

  writeFileSync('scripts/urdu-dict.json', JSON.stringify(merged, null, 2), 'utf8');
  const translatedCount = Object.keys(en).filter((k) => merged[k] !== undefined).length;
  console.log(`Done. UR coverage: ${translatedCount}/${Object.keys(en).length} keys. i18n.ts updated.`);
}

main().catch((err) => {
  console.error('FATAL:', err.message);
  process.exit(1);
});
