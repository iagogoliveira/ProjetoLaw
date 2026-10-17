const STORAGE_KEY = 'lexflow.pendingInvitation';

export const INVITATION_PATH = '/convite';

/**
 * Token do convite guardado na aba enquanto a pessoa entra ou cria a conta (o login e o cadastro acontecem no
 * servidor de identidade). Na volta do login, o SPA retoma a página do convite. Só no `sessionStorage`.
 */
export const PendingInvitation = {
  save(token: string): void {
    try {
      sessionStorage.setItem(STORAGE_KEY, token);
    } catch {
      // Sem armazenamento da aba: a pessoa abre o link do e-mail de novo depois de entrar.
    }
  },

  peek(): string | null {
    try {
      return sessionStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  },

  clear(): void {
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nada guardado.
    }
  },
};
