import { computed, inject, Injectable, signal } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { AccessStore } from '../organization/access-store';

/** Dados do id token usados pela interface (o perfil completo vem de `GET /api/v1/me`). */
export interface SignedInUser {
  readonly name: string;
  readonly email: string;
}

/**
 * Estado de autenticação do SPA. `initialize()` roda uma vez no bootstrap: processa o retorno do login
 * (`/auth/callback`) ou restaura a sessão da aba (`sessionStorage`). Se o servidor de identidade estiver fora do
 * ar, o SPA continua de pé, sem login.
 */
@Injectable({ providedIn: 'root' })
export class AuthSession {
  private readonly oidc = inject(OidcSecurityService);
  private readonly config = inject(APP_CONFIG);
  private readonly access = inject(AccessStore);
  private readonly state = signal<SignedInUser | null>(null);
  private initialization?: Promise<void>;

  readonly user = this.state.asReadonly();
  readonly isAuthenticated = computed(() => this.state() !== null);

  /** Cadastro na página do servidor de identidade (o SPA não recebe senha). */
  readonly registerUrl = computed(() => `${this.config.authority}/conta/cadastro`);

  /** Cadastro a partir de um convite, com o e-mail convidado já preenchido. */
  registerUrlFor(email: string): string {
    return `${this.registerUrl()}?email=${encodeURIComponent(email)}`;
  }

  initialize(): Promise<void> {
    this.initialization ??= firstValueFrom(this.oidc.checkAuth())
      .then((result) => this.state.set(result.isAuthenticated ? toUser(result.userData) : null))
      .catch(() => this.state.set(null));
    return this.initialization;
  }

  signIn(): void {
    this.oidc.authorize();
  }

  /** Encerra a sessão no servidor (revoga os tokens) e volta para a página inicial do SPA. */
  async signOut(): Promise<void> {
    await firstValueFrom(this.oidc.logoff()).catch(() => undefined);
    this.state.set(null);
    this.access.reset();
  }
}

function toUser(userData: unknown): SignedInUser {
  const claims = (userData ?? {}) as Record<string, unknown>;
  return { name: String(claims['name'] ?? ''), email: String(claims['email'] ?? '') };
}
