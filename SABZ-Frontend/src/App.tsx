import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from '@/hooks/useAuth';
import { AppShell } from '@/components/layout/AppShell';
import { FloatingChatWidget } from '@/components/chat/FloatingChatWidget';
import { ProtectedRoute } from '@/components/routes/ProtectedRoute';
import { LoginPage } from '@/pages/auth/LoginPage';
import { RegisterPage } from '@/pages/auth/RegisterPage';
import { DashboardPage } from '@/pages/dashboard/DashboardPage';
import { FarmsPage } from '@/pages/farms/FarmsPage';
import { FarmCreatePage } from '@/pages/farms/FarmCreatePage';
import { FarmEditPage } from '@/pages/farms/FarmEditPage';
import { FarmDetailPage } from '@/pages/farms/FarmDetailPage';
import { CropsPage } from '@/pages/crops/CropsPage';
import { CropCreatePage } from '@/pages/crops/CropCreatePage';
import { CropEditPage } from '@/pages/crops/CropEditPage';
import { WeatherPage } from '@/pages/weather/WeatherPage';
import { DiseaseDetectionPage } from '@/pages/disease/DiseaseDetectionPage';
import { MonitoringPage } from '@/pages/monitoring/MonitoringPage';
import { CropMonitoringPage } from '@/pages/monitoring/CropMonitoringPage';
import { NotificationsPage } from '@/pages/notifications/NotificationsPage';
import { FinancialLedgerPage } from '@/pages/financial/FinancialLedgerPage';
import { TransactionFormPage } from '@/pages/financial/TransactionFormPage';
import { CropFinancialHealthPage } from '@/pages/financial-health/CropFinancialHealthPage';
import { FarmDashboardPage } from '@/pages/farm-dashboard/FarmDashboardPage';
import { AgronomistPage } from '@/pages/agronomist/AgronomistPage';
import { CommunityPostPage } from '@/pages/community/CommunityPostPage';
import { MarketplaceListingPage } from '@/pages/marketplace/MarketplaceListingPage';
import { MarketplaceFormPage } from '@/pages/marketplace/MarketplaceFormPage';
import { ConversationPage } from '@/pages/inbox/ConversationPage';
import { KisanNetworkPage } from '@/pages/kisan/KisanNetworkPage';
import { SelectFarmPage } from '@/pages/utilities/SelectFarmPage';
import { InputCalculatorPage } from '@/pages/calculator/InputCalculatorPage';
import { CropPricesPage } from '@/pages/crop-prices/CropPricesPage';
import { CropPriceDetailPage } from '@/pages/crop-prices/CropPriceDetailPage';
import { Camera } from 'lucide-react';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          {/* Public routes */}
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />

          {/* Protected routes */}
          <Route path="/dashboard" element={
            <ProtectedRoute><AppShell><DashboardPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms" element={
            <ProtectedRoute><AppShell><FarmsPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/new" element={
            <ProtectedRoute><AppShell><FarmCreatePage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId" element={
            <ProtectedRoute><AppShell><FarmDetailPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/edit" element={
            <ProtectedRoute><AppShell><FarmEditPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/crops" element={
            <ProtectedRoute><AppShell><CropsPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/crops/new" element={
            <ProtectedRoute><AppShell><CropCreatePage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/crops/:cropId/edit" element={
            <ProtectedRoute><AppShell><CropEditPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/weather" element={
            <ProtectedRoute><AppShell><WeatherPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/disease-detection" element={
            <ProtectedRoute><AppShell><DiseaseDetectionPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/crops/:cropId/monitoring" element={
            <ProtectedRoute><AppShell><CropMonitoringPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/monitoring" element={
            <ProtectedRoute><AppShell><MonitoringPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/notifications" element={
            <ProtectedRoute><AppShell><NotificationsPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/financial" element={
            <ProtectedRoute><AppShell><FinancialLedgerPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/financial/new" element={
            <ProtectedRoute><AppShell><TransactionFormPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/financial/:transactionId/edit" element={
            <ProtectedRoute><AppShell><TransactionFormPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/farms/:farmId/crops/:cropId/financial-health" element={
            <ProtectedRoute><AppShell><CropFinancialHealthPage /></AppShell></ProtectedRoute>
          } />

          {/* Prompt 12 - Farm Dashboard */}
          <Route path="/farms/:farmId/dashboard" element={
            <ProtectedRoute><AppShell><FarmDashboardPage /></AppShell></ProtectedRoute>
          } />
          {/* Prompt 13 - AI Agronomist */}
          <Route path="/farms/:farmId/agronomist" element={
            <ProtectedRoute><AppShell><AgronomistPage /></AppShell></ProtectedRoute>
          } />
          {/* Kisan Network - community + marketplace + inbox merged (Prompts 14-15) */}
          <Route path="/kisan" element={
            <ProtectedRoute><AppShell><KisanNetworkPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/kisan/post/:postId" element={
            <ProtectedRoute><AppShell><CommunityPostPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/kisan/listing/new" element={
            <ProtectedRoute><AppShell><MarketplaceFormPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/kisan/listing/:listingId" element={
            <ProtectedRoute><AppShell><MarketplaceListingPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/kisan/listing/:listingId/edit" element={
            <ProtectedRoute><AppShell><MarketplaceFormPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/kisan/conversation/:conversationId" element={
            <ProtectedRoute><AppShell><ConversationPage /></AppShell></ProtectedRoute>
          } />
          {/* Legacy route redirects */}
          <Route path="/community" element={<Navigate to="/kisan" replace />} />
          <Route path="/community/:postId" element={<Navigate to="/kisan" replace />} />
          <Route path="/marketplace" element={<Navigate to="/kisan" replace />} />
          <Route path="/marketplace/:listingId" element={<Navigate to="/kisan" replace />} />
          <Route path="/inbox" element={<Navigate to="/kisan?tab=messages" replace />} />
          <Route path="/inbox/:conversationId" element={<Navigate to="/kisan?tab=messages" replace />} />
          {/* Utilities - farm-scoped tool entry points */}
          <Route path="/utilities/disease-detection" element={
            <ProtectedRoute><AppShell><SelectFarmPage pathTemplate="/farms/:farmId/disease-detection" icon={Camera} /></AppShell></ProtectedRoute>
          } />
          {/* Prompt 16 - Input Calculator */}
          <Route path="/input-calculator" element={
            <ProtectedRoute><AppShell><InputCalculatorPage /></AppShell></ProtectedRoute>
          } />
          {/* Prompt 17 - Crop Prices */}
          <Route path="/crop-prices" element={
            <ProtectedRoute><AppShell><CropPricesPage /></AppShell></ProtectedRoute>
          } />
          <Route path="/crop-prices/:cropName" element={
            <ProtectedRoute><AppShell><CropPriceDetailPage /></AppShell></ProtectedRoute>
          } />

          {/* Default redirect */}
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="*" element={<Navigate to="/dashboard" replace />} />
        </Routes>

        {/* Global AI agronomist chat — mounted once at router level so the
            conversation survives route changes (each route has its own AppShell). */}
        <FloatingChatWidget />
      </AuthProvider>
    </BrowserRouter>
  );
}
