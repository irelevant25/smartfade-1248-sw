import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ConsoleConnectionService } from '../../core/console-connection.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { ViewService, type AppView } from '../../core/view.service';
import { ShowMenuComponent } from '../show-menu/show-menu.component';
import { canConnect, canDisconnect, describeMode, describeStateAtConnect, describeStatus } from './connection-view';

/**
 * Top bar: which mode the app is in (always visible, never ambiguous), whether the console is
 * connected, the view switch (LIVE, PATCH, STACK / SEQ), the connect / disconnect keys, the Show
 * menu (the show's file: open a file, save for the card), what is known about the show and
 * firmware, and the last error.
 */
@Component({
  selector: 'sf-connection-bar',
  imports: [ShowMenuComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './connection-bar.component.html',
  styleUrl: './connection-bar.component.scss',
})
export class ConnectionBarComponent {
  private readonly _state = inject(ConsoleStateService);
  private readonly _connection = inject(ConsoleConnectionService);
  private readonly _views = inject(ViewService);

  /** Which view is shown: LIVE (the console), PATCH or STACK / SEQ. */
  protected readonly view = this._views.view;

  protected readonly mode = computed(() => describeMode(this._state.mode()));
  /**
   * The real console with live control switched on (safety check SC-2): the hardware device reports commands only
   * then, and the UI says so as loudly as the mode itself (SC-2 item 6).
   */
  protected readonly liveControl = computed(() => this._state.mode() === 'hardware' && this._state.supportedCommands().length > 0);
  /**
   * The backend requests the real console's state at every Connect (safety check SC-4: SmartSoft's start-up pair and the
   * download it starts): STATE AT CONNECT beside HARDWARE, as loud, and the request's progress or why it was not sent.
   */
  protected readonly stateAtConnect = computed(() => this._state.mode() === 'hardware' && this._state.stateAtConnect() !== undefined);
  protected readonly stateAtConnectNote = computed(() =>
    this.stateAtConnect() ? describeStateAtConnect(this._state.stateAtConnect(), this._state.connection()?.status) : undefined,
  );
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
