import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { authApi } from '@/api/authApi';
import { parseApiError } from '@/api/client';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Alert } from '@/components/ui/Alert';
import { useAuth } from '@/hooks/useAuth';
import { t } from '@/lib/i18n';
import { Sprout, CheckCircle } from 'lucide-react';

export function RegisterPage() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [form, setForm] = useState({
    fullName: '',
    email: '',
    phoneNumber: '',
    password: '',
    confirmPassword: '',
    preferredLanguage: 'English',
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [success, setSuccess] = useState(false);

  const set = (field: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setFieldErrors({});
    setLoading(true);

    // Client validation: at least email or phone
    if (!form.email && !form.phoneNumber) {
      setError('Please provide either an email address or a phone number.');
      setLoading(false);
      return;
    }

    try {
      const res = await authApi.register({
        fullName: form.fullName,
        email: form.email || null,
        phoneNumber: form.phoneNumber || null,
        password: form.password,
        confirmPassword: form.confirmPassword,
        preferredLanguage: form.preferredLanguage,
      });

      if (res.success) {
        // Backend issues a token with registration — log the farmer straight in
        if (res.token && res.user) {
          login(res.token, res.user);
          navigate('/', { replace: true });
          return;
        }
        setSuccess(true);
        setTimeout(() => navigate('/login'), 2500);
      } else {
        setError(res.message || 'Registration failed.');
      }
    } catch (err) {
      const parsed = parseApiError(err);
      setError(parsed.message);
      if (parsed.fieldErrors) {
        setFieldErrors(parsed.fieldErrors);
      }
    } finally {
      setLoading(false);
    }
  };

  if (success) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-earth-50 px-6">
        <div className="text-center animate-fade-in">
          <div className="mx-auto mb-4 h-16 w-16 rounded-full bg-emerald-100 flex items-center justify-center">
            <CheckCircle className="h-8 w-8 text-emerald-600" />
          </div>
          <h1 className="text-2xl font-bold text-gray-900 mb-2">Account Created!</h1>
          <p className="text-gray-500">Redirecting to login...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex">
      {/* Left side - decorative */}
      <div className="hidden lg:flex lg:w-1/2 bg-gradient-to-br from-primary-800 via-primary-700 to-primary-900 relative overflow-hidden">
        <div className="absolute inset-0 opacity-10">
          <svg className="absolute -top-20 -left-20 h-96 w-96 text-white" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          </svg>
        </div>
        <div className="relative z-10 flex flex-col justify-center px-12 xl:px-20">
          <div className="flex items-center gap-3 mb-8">
            <div className="h-12 w-12 rounded-2xl bg-white/20 flex items-center justify-center backdrop-blur-sm">
              <Sprout className="h-7 w-7 text-white" />
            </div>
            <div>
              <h1 className="text-3xl font-bold text-white">{t('app.name')}</h1>
              <p className="text-primary-200 text-sm">{t('app.tagline')}</p>
            </div>
          </div>
          <h2 className="text-4xl font-bold text-white mb-4 leading-tight">
            Join the farming<br />revolution
          </h2>
          <p className="text-primary-100 text-lg max-w-md">
            Get AI-powered crop recommendations, real-time weather intelligence, and smart farm management tools.
          </p>
        </div>
      </div>

      {/* Right side - form */}
      <div className="flex-1 flex items-center justify-center px-6 py-12 lg:px-20">
        <div className="w-full max-w-md">
          <div className="lg:hidden flex items-center gap-3 mb-8 justify-center">
            <div className="h-10 w-10 rounded-xl bg-primary-700 flex items-center justify-center">
              <Sprout className="h-5 w-5 text-white" />
            </div>
            <span className="text-2xl font-bold text-gray-900">{t('app.name')}</span>
          </div>

          <div className="text-center mb-8">
            <h1 className="text-2xl font-bold text-gray-900">{t('auth.register')}</h1>
            <p className="text-sm text-gray-500 mt-1">Create your SABZ farmer account</p>
          </div>

          {error && (
            <Alert variant="error" className="mb-6" dismissible>
              {error}
            </Alert>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <Input
              label={t('auth.fullName')}
              placeholder={t('auth.fullNamePlaceholder')}
              value={form.fullName}
              onChange={set('fullName')}
              required
              error={fieldErrors.FullName?.[0]}
              autoComplete="name"
            />

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Input
                label={`${t('auth.email')} (optional)`}
                placeholder={t('auth.emailPlaceholder')}
                type="email"
                value={form.email}
                onChange={set('email')}
                error={fieldErrors.Email?.[0]}
                autoComplete="email"
              />
              <Input
                label={`${t('auth.phone')} (optional)`}
                placeholder={t('auth.phonePlaceholder')}
                type="tel"
                value={form.phoneNumber}
                onChange={set('phoneNumber')}
                error={fieldErrors.PhoneNumber?.[0]}
                autoComplete="tel"
              />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Input
                label={t('auth.password')}
                placeholder="Min 8 characters"
                type="password"
                value={form.password}
                onChange={set('password')}
                required
                error={fieldErrors.Password?.[0]}
                autoComplete="new-password"
              />
              <Input
                label={t('auth.confirmPassword')}
                placeholder={t('auth.confirmPasswordPlaceholder')}
                type="password"
                value={form.confirmPassword}
                onChange={set('confirmPassword')}
                required
                error={fieldErrors.ConfirmPassword?.[0]}
                autoComplete="new-password"
              />
            </div>

            <Select
              label={t('auth.language')}
              value={form.preferredLanguage}
              onChange={set('preferredLanguage')}
              options={[
                { value: 'English', label: 'English' },
                { value: 'Urdu', label: 'اردو (Urdu)' },
              ]}
            />

            <Button type="submit" loading={loading} className="w-full" size="lg">
              {t('auth.register')}
            </Button>
          </form>

          <p className="mt-6 text-center text-sm text-gray-500">
            {t('auth.loginPrompt')}{' '}
            <Link to="/login" className="text-primary-700 font-semibold hover:text-primary-800">
              {t('auth.login')}
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
