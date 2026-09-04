import { useState, useEffect, type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '@/hooks/useAuth';
import { notificationApi } from '@/api/notificationApi';
import { t, getLanguage, setLanguage, isRtl } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import {
  LayoutDashboard,
  MapPinned,
  LogOut,
  Menu,
  X,
  Globe,
  User,
  Sprout,
  Bell,
  ClipboardCheck,
  Users,
  TrendingUp,
  Calculator,
  Camera,
} from 'lucide-react';

interface AppShellProps {
  children: ReactNode;
}

/**
 * 4-section navigation (overhauled):
 *   Dashboard | My Farms | Kisan Network | Utilities
 * Group headers keep the number of visible choices small while every
 * feature stays one click away.
 */
interface NavEntry {
  to: string;
  label: string;
  icon: typeof LayoutDashboard;
}

interface NavSection {
  titleKey: string;
  entries: NavEntry[];
}

const navSections: NavSection[] = [
  {
    titleKey: 'nav.section.dashboard',
    entries: [
      { to: '/dashboard', label: 'nav.dashboard', icon: LayoutDashboard },
    ],
  },
  {
    titleKey: 'nav.section.myFarms',
    entries: [
      { to: '/farms', label: 'nav.farms', icon: MapPinned },
      { to: '/monitoring', label: 'monitoring.title', icon: ClipboardCheck },
      { to: '/notifications', label: 'notifications.title', icon: Bell },
    ],
  },
  {
    titleKey: 'nav.section.kisanNetwork',
    entries: [
      { to: '/kisan', label: 'nav.kisanNetwork', icon: Users },
    ],
  },
  {
    titleKey: 'nav.section.utilities',
    entries: [
      { to: '/utilities/disease-detection', label: 'nav.diseaseCamera', icon: Camera },
      { to: '/input-calculator', label: 'nav.inputCalculator', icon: Calculator },
      { to: '/crop-prices', label: 'nav.mandiRates', icon: TrendingUp },
    ],
  },
];

export function AppShell({ children }: AppShellProps) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const lang = getLanguage();

  useEffect(() => {
    notificationApi.getUnreadCount()
      .then((r) => setUnreadCount(r.count))
      .catch(() => {});
  }, []);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const toggleLanguage = () => {
    const next = lang === 'en' ? 'ur' : 'en';
    setLanguage(next);
    // Re-render by navigating
    window.location.reload();
  };

  const renderNav = (onNavigate?: () => void) => (
    <nav className="flex-1 px-3 py-4 space-y-4 overflow-y-auto">
      {navSections.map((section) => (
        <div key={section.titleKey}>
          <p className="px-3 mb-1.5 text-[10px] font-semibold text-gray-400 uppercase tracking-wider">
            {t(section.titleKey)}
          </p>
          <div className="space-y-1">
            {section.entries.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors',
                    isActive
                      ? 'bg-primary-50 text-primary-800'
                      : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900',
                  )
                }
              >
                <item.icon className="h-5 w-5 shrink-0" />
                {t(item.label)}
              </NavLink>
            ))}
          </div>
        </div>
      ))}
    </nav>
  );

  const renderUser = () => (
    <div className="p-3 border-t border-gray-100">
      <div className="flex items-center gap-3 px-3 py-2.5 rounded-xl bg-gray-50">
        <div className="h-8 w-8 rounded-full bg-primary-100 flex items-center justify-center shrink-0">
          <User className="h-4 w-4 text-primary-700" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-medium text-gray-900 truncate">{user?.fullName}</p>
          <p className="text-xs text-gray-500 truncate">{user?.email || user?.phoneNumber}</p>
        </div>
      </div>
      <button
        onClick={handleLogout}
        className="w-full mt-2 flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium text-red-600 hover:bg-red-50 transition-colors"
      >
        <LogOut className="h-3.5 w-3.5" />
        {t('nav.logout')}
      </button>
    </div>
  );

  return (
    <div className="min-h-screen bg-earth-50 flex" dir={isRtl() ? 'rtl' : 'ltr'}>
      {/* Sidebar - Desktop */}
      <aside className={`hidden lg:flex lg:w-64 lg:flex-col lg:fixed lg:inset-y-0 bg-white border-gray-200 ${isRtl() ? 'lg:right-0 lg:border-l' : 'lg:left-0 lg:border-r'}`}>
        {/* Logo */}
        <div className="h-16 flex items-center gap-3 px-6 border-b border-gray-100">
          <div className="h-9 w-9 rounded-xl bg-primary-700 flex items-center justify-center">
            <Sprout className="h-5 w-5 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-gray-900 tracking-tight">{t('app.name')}</h1>
            <p className="text-[10px] text-gray-500 -mt-0.5 font-medium uppercase tracking-wider">
              {t('app.tagline')}
            </p>
          </div>
        </div>

        {renderNav()}
        {renderUser()}
      </aside>

      {/* Mobile sidebar overlay */}
      {sidebarOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="fixed inset-0 bg-black/40" onClick={() => setSidebarOpen(false)} />
          <div className={`fixed inset-y-0 w-72 bg-white shadow-xl animate-fade-in flex flex-col ${isRtl() ? 'right-0' : 'left-0'}`}>
            {/* Mobile header */}
            <div className="h-16 flex items-center justify-between px-6 border-b border-gray-100">
              <div className="flex items-center gap-3">
                <div className="h-9 w-9 rounded-xl bg-primary-700 flex items-center justify-center">
                  <Sprout className="h-5 w-5 text-white" />
                </div>
                <span className="text-lg font-bold text-gray-900">{t('app.name')}</span>
              </div>
              <button
                onClick={() => setSidebarOpen(false)}
                className="p-2 rounded-lg text-gray-400 hover:bg-gray-100"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            {renderNav(() => setSidebarOpen(false))}

            {/* Mobile user */}
            <div className="border-t border-gray-100 bg-white">
              {renderUser()}
            </div>
          </div>
        </div>
      )}

      {/* Main content */}
      <div className={`flex-1 ${isRtl() ? 'lg:mr-64' : 'lg:ml-64'}`}>
        {/* Top bar */}
        <header className="sticky top-0 z-30 h-16 bg-white/80 backdrop-blur-md border-b border-gray-100 flex items-center px-4 lg:px-8 gap-4">
          <button
            onClick={() => setSidebarOpen(true)}
            className={`lg:hidden p-2 rounded-lg text-gray-500 hover:bg-gray-100 ${isRtl() ? '-mr-2' : '-ml-2'}`}
            aria-label="Open menu"
          >
            <Menu className="h-5 w-5" />
          </button>

          {/* Breadcrumb-like page indicator (placeholder for current page) */}
          <div className="flex-1" />

          {/* Notification bell */}
          <button
            onClick={() => navigate('/notifications')}
            className="relative p-2 rounded-lg text-gray-500 hover:bg-gray-100 hover:text-gray-700 transition-colors"
            title={t('notifications.title')}
          >
            <Bell className="h-5 w-5" />
            {unreadCount > 0 && (
              <span className={`absolute -top-0.5 h-4.5 w-4.5 min-w-[18px] flex items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white ring-2 ring-white ${isRtl() ? '-left-0.5' : '-right-0.5'}`}>
                {unreadCount > 99 ? '99+' : unreadCount}
              </span>
            )}
          </button>

          {/* Language toggle (single app-wide control, header only) */}
          <button
            onClick={toggleLanguage}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium text-gray-500 hover:bg-gray-100 hover:text-gray-700 transition-colors"
          >
            <Globe className="h-3.5 w-3.5" />
            {lang === 'en' ? 'اردو' : 'EN'}
          </button>
        </header>

        {/* Page content */}
        <main className="p-4 lg:p-8 max-w-7xl mx-auto">{children}</main>
      </div>
    </div>
  );
}
