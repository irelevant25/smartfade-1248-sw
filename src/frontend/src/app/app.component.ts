import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ChannelGridComponent } from './components/channel-grid/channel-grid.component';
import { ConnectionBarComponent } from './components/connection-bar/connection-bar.component';
import { CrossfaderComponent } from './components/crossfader/crossfader.component';
import { FaderModeKeysComponent } from './components/fader-mode-keys/fader-mode-keys.component';
import { IndependentsPanelComponent } from './components/independents-panel/independents-panel.component';
import { MastersPanelComponent } from './components/masters-panel/masters-panel.component';
import { MemoryGridComponent } from './components/memory-grid/memory-grid.component';
import { OutputMetersComponent } from './components/output-meters/output-meters.component';
import { RecordKeysComponent } from './components/record-keys/record-keys.component';
import { ShowFilePageComponent } from './components/show-file-page/show-file-page.component';
import { ViewService } from './core/view.service';

/**
 * The single page, laid out like the console: connection bar on top; channels (two banks) with
 * the memories below them on the left; then, in the owner's order (2026-11-02), the FADERS keys,
 * the crossfader, the record keys, and BUMPS / SOLO, MASTER / BLACKOUT with the independents in
 * their own section underneath on the right; the DMX outputs along the bottom. The show-file
 * viewer takes the main area instead when the bar's page switch says so.
 */
@Component({
  selector: 'sf-root',
  imports: [
    ConnectionBarComponent,
    ChannelGridComponent,
    MemoryGridComponent,
    FaderModeKeysComponent,
    CrossfaderComponent,
    RecordKeysComponent,
    MastersPanelComponent,
    IndependentsPanelComponent,
    OutputMetersComponent,
    ShowFilePageComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  /** The console page is the default; the show-file viewer (E10) replaces the main area when chosen in the bar. */
  protected readonly view = inject(ViewService).view;
}
