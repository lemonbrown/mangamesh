export default function StorageBar({ usedMb, totalMb }: { usedMb: number; totalMb: number }) {
    const percentage = Math.min(100, Math.max(0, (usedMb / totalMb) * 100));
    const usedGb = (usedMb / 1024).toFixed(2);
    const totalGb = (totalMb / 1024).toFixed(2);

    return (
        <div className="w-full">
            <div className="flex justify-between text-sm mb-1">
                <span className="font-medium text-gray-700">Storage Usage</span>
                <span className="text-gray-500">{usedGb} GB / {totalGb} GB</span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2.5 overflow-hidden">
                <div
                    className="bg-blue-600 h-2.5 rounded-full"
                    style={{ width: `${percentage}%` }}
                ></div>
            </div>
        </div>
    );
}
