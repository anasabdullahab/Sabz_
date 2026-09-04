/* ─── Auth Types ─────────────────────────────────────────────────── */
export interface LoginRequest {
  identifier: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email?: string | null;
  phoneNumber?: string | null;
  password: string;
  confirmPassword: string;
  preferredLanguage?: string | null;
}

export interface AuthResponse {
  success: boolean;
  message: string;
  token?: string | null;
  user?: UserResponse | null;
}

export interface UserResponse {
  id: string;
  fullName: string;
  email?: string | null;
  phoneNumber?: string | null;
  preferredLanguage: string;
  role: string;
  createdAt: string;
}

/* ─── Location Types ────────────────────────────────────────────── */
export interface LocationDto {
  id: number;
  name: string;
  nameUrdu?: string | null;
  latitude?: number | null;
  longitude?: number | null;
}

/* ─── Farm Types ────────────────────────────────────────────────── */
export interface CreateFarmDto {
  farmName: string;
  provinceId?: number | null;
  districtId?: number | null;
  tehsilId?: number | null;
  latitude?: number | null;
  longitude?: number | null;
  farmSize: number;
  farmSizeUnit: string;
  soilType?: string | null;
  irrigationType?: string | null;
}

export type UpdateFarmDto = CreateFarmDto;

export interface FarmResponseDto {
  id: string;
  farmName: string;
  provinceId: number;
  provinceName: string;
  districtId: number;
  districtName: string;
  tehsilId: number;
  tehsilName: string;
  latitude?: number | null;
  longitude?: number | null;
  farmSize: number;
  farmSizeUnit: string;
  soilType?: string | null;
  irrigationType?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

/* ─── Crop Types ────────────────────────────────────────────────── */
export interface CreateCropDto {
  cropName: string;
  cropCatalogId?: number | null;
  season: string;
  plantingDate?: string | null;
  harvestDate?: string | null;
  growthStage?: string | null;
  previousCrop?: string | null;
  status?: string | null;
}

export type UpdateCropDto = CreateCropDto;

export interface CropResponseDto {
  id: string;
  farmId: string;
  cropCatalogId?: number | null;
  cropName: string;
  season: string;
  plantingDate?: string | null;
  harvestDate?: string | null;
  growthStage?: string | null;
  previousCrop?: string | null;
  status: string;
  createdAt: string;
  updatedAt?: string | null;
}

/* ─── Weather Types ─────────────────────────────────────────────── */
export interface CurrentWeatherDto {
  temperature?: number | null;
  apparentTemperature?: number | null;
  relativeHumidity?: number | null;
  precipitation?: number | null;
  rain?: number | null;
  windSpeed?: number | null;
  windDirection?: number | null;
  windGusts?: number | null;
  cloudCover?: number | null;
  weatherCode?: number | null;
  isDay?: boolean | null;
  observationTime?: string | null;
}

export interface DailyForecastDto {
  date: string;
  tempMin?: number | null;
  tempMax?: number | null;
  precipitation?: number | null;
  precipitationProbability?: number | null;
  rain?: number | null;
  windSpeed?: number | null;
  weatherCode?: number | null;
  et0?: number | null;
  sunrise?: string | null;
  sunset?: string | null;
  soilTemperature?: number | null;
  soilMoisture?: number | null;
}

export interface ForecastDto {
  timezone?: string | null;
  days: DailyForecastDto[];
}

export interface WeatherResponseDto {
  farmId: string;
  latitude: number;
  longitude: number;
  coordinateSource: string;
  locationName?: string | null;
  source: string;
  retrievedAt: string;
  isStale: boolean;
  staleWarning?: string | null;
  current?: CurrentWeatherDto | null;
  forecast?: ForecastDto | null;
  units: WeatherUnitsDto;
}

export interface ReverseGeocodeDto {
  name: string;
  admin1?: string | null;
  country?: string | null;
  displayLabel: string;
}

export interface WeatherUnitsDto {
  temperature: string;
  windSpeed: string;
  precipitation: string;
  humidity: string;
  soilMoisture: string;
}

/* Weather preview (tehsil-based, no farm required — dashboard onboarding) */
export interface WeatherPreviewDto {
  locationName: string;
  latitude: number;
  longitude: number;
  source: string;
  retrievedAt: string;
  current?: CurrentWeatherDto | null;
  forecast?: ForecastDto | null;
}

/* Smart weather action alerts (GET /api/farms/{farmId}/weather/alerts) */
export interface WeatherAlertDto {
  type: string;
  severity: string;
  title: string;
  message: string;
  when: string;
  trigger: string;
}

export interface WeatherAlertsResponseDto {
  farmId: string;
  userId: string;
  alerts: WeatherAlertDto[];
  evaluatedAt: string;
  disclaimer: string;
}

/* Local crop knowledge base entry (GET /api/crop-knowledge) */
export interface CropKnowledgeEntryDto {
  name: string;
  nameUrdu: string;
  category: string;
  season: string;
  maturityDays: number;
  suitableSoil: string[];
  nitrogenImpact: string;
  waterRequirement: string;
  stageTimeline: {
    germination: [number, number];
    vegetative: [number, number];
    flowering: [number, number];
    maturity: [number, number];
  };
}

/* ─── Disease Detection Types (Prompt 6) ──────────────────────────── */
export interface DiseaseDetectionResponseDto {
  farmId: string;
  cropId?: string | null;
  cropContext?: DiseaseCropContextDto | null;
  imageAssessment: DiseaseImageAssessmentDto;
  diseaseAssessment?: DiseaseAssessmentDto | null;
  advice?: DiseaseAdviceDto | null;
  missingData: string[];
  isLocalFallback: boolean;
  provider: DiseaseProviderInfoDto;
  evaluatedAt: string;
  disclaimer: string;
}

export interface DiseaseCropContextDto {
  cropName: string;
  season: string;
  growthStage?: string | null;
  plantingDate?: string | null;
  catalogName?: string | null;
  catalogCategory?: string | null;
}

export interface DiseaseImageAssessmentDto {
  imageAccepted: boolean;
  isPlantImage: boolean;
  plantConfidence?: number | null;
  message?: string | null;
  width: number;
  height: number;
  format?: string | null;
  possiblyBlurry: boolean;
}

export interface DiseaseAssessmentDto {
  detected: boolean;
  assessmentLevel: string;
  crop?: string | null;
  disease?: string | null;
  confidence?: number | null;
  severity?: string | null;
  explanation?: string | null;
  assessmentSource: string;
  commonDiseasesForCrop: string[];
}

export interface DiseaseAdviceDto {
  summary: string;
  recommendedActions: string[];
  prevention: string[];
  monitoring: string[];
  adviceSources: string[];
}

export interface DiseaseProviderInfoDto {
  name: string;
  model: string;
  version?: string | null;
  configured: boolean;
}

/* ─── Monitoring Types (Prompt 7) ──────────────────────────────── */
export interface MonitoringCheckDto {
  id: string;
  cropId: string;
  cropName: string;
  cropCatalogName?: string | null;
  farmId: string;
  farmName?: string | null;
  scheduledDate: string;
  status: string;
  title: string;
  description: string;
  inspectionItems: string[];
  priority: string;
  observation?: string | null;
  farmerNotes?: string | null;
  completedAt?: string | null;
  skippedAt?: string | null;
  photoAnalysisRecommended: boolean;
}

export interface CompleteMonitoringCheckRequestDto {
  observation: string;
  notes?: string | null;
}

export interface SkipMonitoringCheckRequestDto {
  notes?: string | null;
}

export interface MonitoringCompletionResponseDto {
  check: MonitoringCheckDto;
  photoAnalysisRecommended: boolean;
  nextAction: string;
  observationNote: string;
}

export interface MonitoringGenerationResultDto {
  cropId: string;
  hasPlantingDate: boolean;
  plantingDate?: string | null;
  rulesApplied: number;
  checksCreated: number;
  existingChecks: number;
  checks: MonitoringCheckDto[];
  notes: string[];
}

/* ─── Notification Types (Prompt 8) ────────────────────────────── */
export interface NotificationDto {
  id: string;
  title: string;
  message: string;
  category: string;
  referenceType: string;
  referenceId: string;
  isRead: boolean;
  createdAt: string;
  readAt?: string | null;
}

export interface UnreadCountResponseDto {
  count: number;
}

export interface MarkAllReadResponseDto {
  markedRead: number;
}

/* ─── Financial Types (Prompt 9) ───────────────────────────────── */
export interface CreateFinancialTransactionDto {
  transactionType: string;
  category: string;
  amount: number;
  transactionDate?: string | null;
  cropId?: string | null;
  notes?: string | null;
}

export type UpdateFinancialTransactionDto = CreateFinancialTransactionDto;

export interface FinancialTransactionResponseDto {
  id: string;
  farmId: string;
  cropId?: string | null;
  cropName?: string | null;
  transactionType: string;
  category: string;
  amount: number;
  transactionDate: string;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface FinancialSummaryResponseDto {
  farmId: string;
  cropId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  totalIncome: number;
  totalExpenses: number;
  netProfitLoss: number;
  transactionCount: number;
}

/* ─── Financial Health Types (Prompt 10) ───────────────────────── */
export interface FinancialHealthSummaryDto {
  farmId: string;
  cropId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  totalIncome: number;
  totalExpense: number;
  netResult: number;
  incomeTransactionCount: number;
  expenseTransactionCount: number;
  totalTransactionCount: number;
  firstTransactionDate?: string | null;
  lastTransactionDate?: string | null;
  numberOfActiveFinancialDays: number;
  cropRelatedTransactionCount: number;
  farmLevelTransactionCount: number;
  healthIndicator: string;
  healthExplanation: string;
}

export interface CategoryBreakdownDto {
  farmId: string;
  cropId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  totalExpense: number;
  totalIncome: number;
  expenses: HealthCategoryDto[];
  income: HealthCategoryDto[];
}

export interface HealthCategoryDto {
  category: string;
  amount: number;
  transactionCount: number;
  percentage: number;
}

export interface FinancialActivityDto {
  farmId: string;
  fromDate?: string | null;
  toDate?: string | null;
  totalIncome: number;
  totalExpense: number;
  netResult: number;
  totalTransactionCount: number;
  periods: FinancialActivityPeriodDto[];
}

export interface FinancialActivityPeriodDto {
  period: string;
  income: number;
  expense: number;
  netResult: number;
  transactionCount: number;
}

export interface FinancialCompletenessDto {
  farmId: string;
  status: string;
  score: number;
  explanation: string;
  limitations: string[];
  disclaimer: string;
  checks: FinancialCompletenessCheckDto[];
}

export interface FinancialCompletenessCheckDto {
  name: string;
  passed: boolean;
  description: string;
}

/* ─── Farm Performance Types (Prompt 11) ────────────────────────── */
export interface RecordedCropPerformanceDto {
  cropId: string;
  cropName: string;
  totalIncome: number;
  totalExpense: number;
  netResult: number;
  incomeTransactionCount: number;
  expenseTransactionCount: number;
  transactionCount: number;
}

export interface PerformanceLimitationDto {
  code: string;
  message: string;
}

export interface FarmPerformanceSummaryDto {
  farmId: string;
  farmName: string;
  fromDate?: string | null;
  toDate?: string | null;
  totalCrops: number;
  activeCrops: number;
  cropsWithFinancialActivity: number;
  cropsWithoutFinancialActivity: number;
  transactionCount: number;
  totalIncome: number;
  totalExpense: number;
  netResult: number;
  bestRecordedCrop?: RecordedCropPerformanceDto | null;
  weakestRecordedCrop?: RecordedCropPerformanceDto | null;
  overallStatus: string;
  statusExplanation: string;
  limitations: PerformanceLimitationDto[];
  disclaimer: string;
}

export interface CropPerformanceDto {
  cropId: string;
  cropName: string;
  status: string;
  transactionCount: number;
  totalIncome: number;
  totalExpense: number;
  netResult: number;
  hasIncomeRecords: boolean;
  hasExpenseRecords: boolean;
  financialDataStatus: string;
}

export interface FarmActivitySummaryDto {
  farmId: string;
  financialTransactionCount: number;
  monitoringCheckCount: number;
  completedMonitoringChecks: number;
  skippedMonitoringChecks: number;
  scheduledMonitoringChecks: number;
  firstRecordedActivity?: string | null;
  latestRecordedActivity?: string | null;
  recordedActivityDays: number;
  explanation: string;
}

/* ─── Farm Dashboard Types (Prompt 12) ─────────────────────────── */
export interface DashboardFarmSectionDto {
  farmId: string;
  farmName: string;
  province: string;
  district: string;
  tehsil: string;
  farmSize: number;
  farmSizeUnit: string;
  soilType?: string | null;
  irrigationType?: string | null;
  hasCoordinates: boolean;
}

export interface DashboardCropItemDto {
  cropId: string;
  cropName: string;
  season: string;
  growthStage?: string | null;
  status: string;
}

export interface DashboardCropsSectionDto {
  totalCrops: number;
  activeCrops: number;
  crops: DashboardCropItemDto[];
}

export interface DashboardMonitoringSectionDto {
  dueChecks: number;
  upcomingChecks: number;
  completedChecks: number;
  skippedChecks: number;
  totalChecks: number;
}

export interface DashboardNotificationsSectionDto {
  unreadCount: number;
  recentNotifications: NotificationDto[];
}

export interface DashboardFinancialSectionDto {
  totalIncome: number;
  totalExpenses: number;
  netResult: number;
  transactionCount: number;
}

export interface DashboardFinancialHealthSectionDto {
  healthIndicator: string;
  healthExplanation: string;
  completenessStatus: string;
  completenessScore: number;
  disclaimer: string;
}

export interface DashboardPerformanceSectionDto {
  overallStatus: string;
  statusExplanation: string;
  netResult: number;
  bestRecordedCrop?: RecordedCropPerformanceDto | null;
  weakestRecordedCrop?: RecordedCropPerformanceDto | null;
}

export interface DashboardWeatherSectionDto {
  source: string;
  retrievedAt: string;
  current?: CurrentWeatherDto | null;
  note: string;
}

export interface DashboardLimitationDto {
  code: string;
  message: string;
}

export interface FarmDashboardDto {
  farm: DashboardFarmSectionDto;
  crops: DashboardCropsSectionDto;
  monitoring: DashboardMonitoringSectionDto;
  notifications: DashboardNotificationsSectionDto;
  financial: DashboardFinancialSectionDto;
  financialHealth: DashboardFinancialHealthSectionDto;
  performance: DashboardPerformanceSectionDto;
  weather?: DashboardWeatherSectionDto | null;
  limitations: DashboardLimitationDto[];
  disclaimer: string;
  generatedAt: string;
}

/* ─── AI Agronomist Types (Prompt 13) ──────────────────────────── */
export interface AgronomistCropContextDto {
  cropName: string;
  season: string;
  growthStage?: string | null;
  status: string;
}

export interface AgronomistFarmContextDto {
  farmId: string;
  farmName: string;
  province: string;
  district: string;
  tehsil: string;
  soilType?: string | null;
  irrigationType?: string | null;
  farmSize: number;
  farmSizeUnit: string;
  activeCrops: AgronomistCropContextDto[];
  weatherIncluded: boolean;
  weatherSummary?: string | null;
}

export interface AgronomistLimitationDto {
  code: string;
  message: string;
}

export interface AgronomistResponseDto {
  question: string;
  answer: string;
  answerSource: string;
  language: string;
  farmContextUsed: AgronomistFarmContextDto;
  limitations: AgronomistLimitationDto[];
  disclaimer: string;
  generatedAt: string;
}

export interface VoiceAgronomistResponseDto extends AgronomistResponseDto {
  transcription: string;
  transcriptionProvider?: string | null;
}

/* ─── Community Types (Prompt 14) ──────────────────────────────── */
export interface CommunityPostResponseDto {
  id: string;
  authorName: string;
  content: string;
  imageUrl?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  commentCount: number;
  isOwnedByCurrentUser: boolean;
}

export interface CommunityCommentResponseDto {
  id: string;
  authorName: string;
  content: string;
  createdAt: string;
  isOwnedByCurrentUser: boolean;
}

export interface CommunityPostDetailDto {
  post: CommunityPostResponseDto;
  comments: CommunityCommentResponseDto[];
}

export interface PagedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/* ─── Marketplace Types (Prompt 15) ────────────────────────────── */
export interface MarketplaceListingSummaryDto {
  id: string;
  title: string;
  category: string;
  listingType: string;
  description: string;
  price: number;
  priceUnit: string;
  location: string;
  condition: string;
  availability: string;
  imageUrl?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  sellerName: string;
  isOwnedByCurrentUser: boolean;
}

export interface MarketplaceListingResponseDto {
  id: string;
  title: string;
  category: string;
  listingType: string;
  description: string;
  price: number;
  priceUnit: string;
  location: string;
  condition: string;
  availability: string;
  imageUrl?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  sellerName: string;
  contactNumber?: string | null;
  isOwnedByCurrentUser: boolean;
}

export interface MarketplacePagedResultDto {
  items: MarketplaceListingSummaryDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CreateMarketplaceListingDto {
  title: string;
  category: string;
  listingType: string;
  description: string;
  price: number;
  priceUnit: string;
  location: string;
  contactNumber: string;
  condition: string;
  availability: string;
  imageUrl?: string | null;
}

export type UpdateMarketplaceListingDto = CreateMarketplaceListingDto;

/* ─── Marketplace Inbox Types (Prompt 15) ──────────────────────── */
export interface MarketplaceConversationSummaryDto {
  conversationId: string;
  listingId: string;
  listingTitle: string;
  otherParticipantName: string;
  latestMessagePreview?: string | null;
  latestMessageAt?: string | null;
  role: string;
}

export interface MarketplaceInboxPagedResultDto {
  items: MarketplaceConversationSummaryDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface MarketplaceMessageDto {
  messageId: string;
  senderName: string;
  content: string;
  createdAt: string;
  isOwnMessage: boolean;
}

export interface MarketplaceConversationDto {
  conversationId: string;
  listingId: string;
  listingTitle: string;
  listingType: string;
  listingPrice: number;
  listingPriceUnit: string;
  buyerName: string;
  sellerName: string;
  currentUserRole: string;
  messages: PagedResultDto<MarketplaceMessageDto>;
}

/* ─── Input Calculator Types (Prompt 16) ───────────────────────── */
export interface InputCalculatorRequestDto {
  cropId?: string | null;
  inputName: string;
  category: string;
  dosageRate: number;
  dosageUnit: string;
  dosageBasis: string;
}

export interface InputCalculatorResponseDto {
  farmId: string;
  cropId?: string | null;
  inputName: string;
  category: string;
  farmArea: number;
  farmAreaUnit: string;
  calculationArea: number;
  calculationAreaUnit: string;
  dosageRate: number;
  dosageUnit: string;
  dosageBasis: string;
  requiredQuantity: number;
  requiredQuantityUnit: string;
  conversionApplied: boolean;
  calculationFormula: string;
  disclaimer: string;
}

/* ─── Crop Price Types (Prompt 17) ─────────────────────────────── */
export interface CropPriceRecordDto {
  cropName: string;
  province: string;
  district: string;
  market: string;
  price: number;
  unit: string;
  priceDate: string;
  source: string;
  dataStatus: string;
  disclaimer: string;
}

export interface CropPricePagedResultDto {
  items: CropPriceRecordDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  dataStatus: string;
  disclaimer: string;
}

export interface CropPriceDetailDto {
  cropName: string;
  cropRecognized: boolean;
  latest?: CropPriceRecordDto | null;
  historicalRecords: CropPriceRecordDto[];
  firstDate?: string | null;
  latestDate?: string | null;
  dataStatus: string;
  message?: string | null;
  disclaimer: string;
}

/* ─── Error Types ───────────────────────────────────────────────── */
export interface ApiErrorResponse {
  message: string;
  errors?: Record<string, string[]>;
  code?: string;
}

/* ─── Language ──────────────────────────────────────────────────── */
export type Language = 'en' | 'ur';
