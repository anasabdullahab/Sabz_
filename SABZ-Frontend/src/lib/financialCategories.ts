import { t } from '@/lib/i18n';

/**
 * Farmer-facing transaction categories (mirrors the backend
 * TransactionCategories.cs allow-list). Labels are translated with a
 * word-splitting fallback for unknown values.
 */

export const EXPENSE_CATEGORIES = [
  'Seeds', 'Fertilizer', 'Labour', 'Irrigation', 'Equipment',
  'Machinery', 'Fuel', 'Transport', 'PestDiseaseManagement', 'OtherExpense',
];

export const INCOME_CATEGORIES = ['CropSale', 'LivestockIncome', 'OtherIncome'];

/** One-tap quick picks rendered as chips above the category select. */
export const QUICK_EXPENSE_CATEGORIES = [
  'Seeds', 'Fertilizer', 'PestDiseaseManagement', 'Labour', 'Fuel',
];

export const QUICK_INCOME_CATEGORIES = INCOME_CATEGORIES;

/** Friendly label for a category ("PestDiseaseManagement" -> "Spray"). */
export function categoryLabel(category: string): string {
  const key = `category.${category}`;
  const translated = t(key);
  return translated === key ? category.replace(/([A-Z])/g, ' $1').trim() : translated;
}
