import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ChannelGridComponent } from './components/channel-grid/channel-grid.component';
import { ConnectionBarComponent } from './components/connection-bar/connection-bar.component';
import { CrossfaderComponent } from './components/crossfader/crossfader.component';
import { MastersPanelComponent } from './components/masters-panel/masters-panel.component';
import { MemoryGridComponent } from './components/memory-grid/memory-grid.component';
import { OutputMetersComponent } from './components/output-meters/output-meters.component';
import { RecordKeysComponent } from './components/record-keys/record-keys.component';

/**
 * The single page, laid out like the console: connection bar on top; channels (two banks) with
 * the memories below them on the left; the record keys and the crossfader in the middle; BUMPS /
 * SOLO, MASTER / BLACKOUT and the independents on the right; the DMX outputs along the bottom.
 */
@Component({
  selector: 'sf-root',
  imports: [
    ConnectionBarComponent,
    ChannelGridComponent,
    MemoryGridComponent,
    RecordKeysComponent,
    CrossfaderComponent,
    MastersPanelComponent,
    OutputMetersComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {}
