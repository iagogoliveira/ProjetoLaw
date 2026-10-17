import { HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { APP_CONFIG } from '../config/app-config';

export const ORGANIZATION_HEADER = 'X-Organization-Id';

const STORAGE_KEY = 'lexflow.activeOrganization';

function read(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

/**
 * Organização ativa da aba (seção 5.3: o interceptor manda `X-Organization-Id` quando o usuário pertence a mais de
 * uma). Fica no `sessionStorage`, como os tokens: sobrevive ao F5 e some ao fechar a aba. O servidor valida a
 * pertença a cada requisição.
 */
@Injectable({ providedIn: 'root' })
export class ActiveOrganization {
  private readonly state = signal<string | null>(read());

  readonly id = this.state.asReadonly();

  set(id: string | null): void {
    this.state.set(id);
    try {
      if (id === null) {
        sessionStorage.removeItem(STORAGE_KEY);
      } else {
        sessionStorage.setItem(STORAGE_KEY, id);
      }
    } catch {
      // Sem armazenamento da aba (modo restrito): vale só até o F5.
    }
  }
}

/** Anexa `X-Organization-Id` às chamadas para a API, e só a elas. */
export const organizationInterceptor: HttpInterceptorFn = (request, next) => {
  const organization = inject(ActiveOrganization).id();
  const apiUrl = inject(APP_CONFIG).apiUrl;
  return organization && request.url.startsWith(apiUrl) && !request.headers.has(ORGANIZATION_HEADER)
    ? next(request.clone({ setHeaders: { [ORGANIZATION_HEADER]: organization } }))
    : next(request);
};
