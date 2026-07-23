import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

import { DashboardMetric } from '../../../core/models/statistics.model';

@Component({
  selector: 'app-metric-card',
  imports: [MatIconModule],
  templateUrl: './metric-card.component.html',
  styleUrl: './metric-card.component.scss'
})
export class MetricCardComponent {
  @Input({ required: true }) metric!: DashboardMetric;
}

