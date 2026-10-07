import { PasswordForm } from './PasswordForm'
import { changeOwnPassword } from './authApi'

export function ChangePasswordPage() {
  return <div className="max-w-md space-y-6"><h1>שינוי סיסמה</h1><PasswordForm own onSave={changeOwnPassword} /></div>
}
