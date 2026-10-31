import { useBranding } from "@/contexts/BrandingContext";
import type { ApexOptions } from "apexcharts";
import Chart from "react-apexcharts";

/** Tiny area chart without axes, used in the live stat cards */
export function Sparkline({ series, max, height = 48, colors }: {
  series: Array<{ name: string; data: number[] }>;
  /** Fixed y maximum (e.g. the limit), auto when omitted */
  max?: number;
  height?: number;
  colors?: string[];
}) {
  const { branding } = useBranding();
  const brand = branding?.primaryColor || "#465FFF";

  const options: ApexOptions = {
    chart: {
      type: "area",
      sparkline: { enabled: true },
      animations: { enabled: false },
      toolbar: { show: false },
      zoom: { enabled: false },
      background: "transparent",
    },
    colors: colors ?? [brand],
    stroke: { curve: "smooth", width: 2 },
    fill: { type: "gradient", gradient: { shadeIntensity: 1, opacityFrom: 0.35, opacityTo: 0, stops: [0, 100] } },
    dataLabels: { enabled: false },
    tooltip: { enabled: false },
    yaxis: { min: 0, max: max && max > 0 ? max : undefined },
    grid: { show: false, padding: { top: 2, bottom: 0, left: 0, right: 0 } },
    legend: { show: false },
  };

  // Keep a flat line instead of an empty chart before the first samples arrive
  const padded = series.map((entry) => ({ ...entry, data: entry.data.length >= 2 ? entry.data : [0, ...entry.data, 0].slice(-2) }));

  return <Chart type="area" height={height} width="100%" series={padded} options={options} />;
}
