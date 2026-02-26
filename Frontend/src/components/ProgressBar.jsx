import React from "react";

const ProgressBar = ({ progress }) => {
  return (
    <div className="w-full mt-2 mb-6">
      <div className="flex justify-between items-end mb-2">
        <span className="text-sm font-semibold text-slate-300 flex items-center">
          <svg
            className="w-4 h-4 mr-1.5 text-indigo-400"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z"
            />
          </svg>
          Cloning Progress
        </span>
        <span className="text-sm font-bold bg-clip-text text-transparent bg-gradient-to-r from-indigo-400 to-cyan-400">
          {progress}%
        </span>
      </div>
      <div className="w-full bg-slate-800/80 backdrop-blur-sm border border-slate-700/50 rounded-full h-3.5 overflow-hidden shadow-inner relative">
        <div
          className="h-full rounded-full transition-all duration-700 ease-out relative bg-gradient-to-r from-indigo-600 via-purple-500 to-cyan-400 shadow-[0_0_15px_rgba(139,92,246,0.6)]"
          style={{ width: `${progress}%` }}
        >
          {/* Animated shine effect */}
          <div className="absolute top-0 left-0 bottom-0 right-0 w-full bg-gradient-to-r from-transparent via-white/30 to-transparent -translate-x-full animate-[shimmer_2s_infinite]" />
        </div>
      </div>
    </div>
  );
};

export default ProgressBar;
