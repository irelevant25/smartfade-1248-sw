import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ChannelGridComponent } from './components/channel-grid/channel-grid.component';
import { ConnectionBarComponent } from './components/connection-bar/connection-bar.component';
import { CrossfaderComponent } from './components/crossfader/crossfader.component';
import { FaderModeKeysComponent } from './components/fader-mode-keys/fader-mode-keys.component';
import { IndependentsPanelComponent } from './components/independents-panel/independents-panel.component';
import { MastersPanelComponent } from './components/masters-panel/masters-panel.component';
import { MemoryGridComponent } from './components/memory-grid/memory-grid.component';
import { OutputMetersComponent } from './components/output-meters/output-meters.component';
import { PatchPageComponent } from './components/patch-page/patch-page.component';
import { RecordKeysComponent } from './components/record-keys/record-keys.component';
import { StackPageComponent } from './components/stack-page/stack-page.component';
import { ViewService } from './core/view.service';

/**
 * The single page, laid out like the console: connection bar on top; channels (two banks) with
 * the memories below them on the left; then, in the owner's order (2026-11-02), the FADERS keys,
 * the crossfader, the record keys, and BUMPS / SOLO, MASTER / BLACKOUT with the independents in
 * their own section underneath on the right; the DMX outputs along the bottom. That is the LIVE
 * view; the bar's view switch puts the PATCH page or the STACK / SEQ page in the main area instead
 * (both read the show held by the `ShowStore`, loaded through the bar's Show menu).
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
    PatchPageComponent,
    StackPageComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  /** LIVE (the console) is the default; PATCH and STACK / SEQ replace the main area when chosen in the bar. */
  protected readonly view = inject(ViewService).view;
}
