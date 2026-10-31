import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ConsoleConnectionService } from '../../core/console-connection.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { ViewService, type AppView } from '../../core/view.service';
import { canConnect, canDisconnect, describeMode, describeStatus } from './connection-view';

/**
 * Top bar: which mode the app is in (always visible, never ambiguous), whether the console is
 * connected, the connect / disconnect keys, what is known about the show and firmware, and the
 * last error.
 */
@Component({
  selector: 'sf-connection-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './connection-bar.component.html',
  styleUrl: './connection-bar.component.scss',
})
export class ConnectionBarComponent {
  private readonly _state = inject(ConsoleStateService);
  private readonly _connection = inject(ConsoleConnectionService);
  private readonly _views = inject(ViewService);

  /** Which page is shown: the console or the show-file viewer (E10). */
  protected readonly view = this._views.view;

  protected readonly mode = computed(() => describeMode(this._state.mode()));
  protected readonly status = computed(() =>
    describeStatus(this._connection.status(), this._state.connection()),
  );
  protected readonly connectEnabled = computed(
    () =>
      canConnect(this._connection.status(), this._state.connection()) && !this._connection.busy(),
  );
  protected readonly disconnectEnabled = computed(
    () =>
      canDisconnect(this._connection.status(), this._state.connection()) &&
      !this._connection.busy(),
  );
  protected readonly showFileName = this._state.showFileName;
  protected readonly firmwareVersion = this._state.firmwareVersion;

  /** The last error reported to this page, else an error the console connection is in. */
  protected readonly error = computed(() => {
    const reported = this._state.lastError();
    if (reported) {
      return { message: reported.message, dismissible: true };
    }
    const connectionError = this._state.connection()?.error;
    return connectionError ? { message: connectionError, dismissible: false } : undefined;
  });

  protected connect(): void {
    void this._connection.connect();
  }

  protected disconnect(): void {
    void this._connection.disconnect();
  }

  protected dismissError(): void {
    this._state.clearError();
  }

  protected show(view: AppView): void {
    this._views.show(view);
  }
}
