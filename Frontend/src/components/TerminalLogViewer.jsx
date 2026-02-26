import React, { useEffect, useRef } from "react";

const TerminalLogViewer = ({ logs }) => {
  const terminalEndRef = useRef(null);

  const scrollToBottom = () => {
    terminalEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    scrollToBottom();
  }, [logs]);

  return (
    <div className="w-full bg-[#050505] border border-slate-700/60 rounded-xl shadow-2xl overflow-hidden mt-6 mb-4 relative group">
      {/* Terminal Header */}
      <div className="bg-slate-800/80 backdrop-blur-md px-4 py-2.5 flex items-center justify-between border-b border-slate-700/50">
        <div className="flex items-center space-x-2">
          <div className="flex space-x-1.5">
            <div className="w-3 h-3 rounded-full bg-red-500/80 shadow-[0_0_5px_rgba(239,68,68,0.5)]"></div>
            <div className="w-3 h-3 rounded-full bg-yellow-500/80 shadow-[0_0_5px_rgba(234,179,8,0.5)]"></div>
            <div className="w-3 h-3 rounded-full bg-green-500/80 shadow-[0_0_5px_rgba(34,197,94,0.5)]"></div>
          </div>
          <div className="ml-4 flex items-center text-xs text-slate-400 font-mono bg-slate-900/50 px-2 py-0.5 rounded border border-slate-700/30">
            <svg
              className="w-3 h-3 mr-1.5 text-slate-500"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"
              />
            </svg>
            playwright-engine.exe
          </div>
        </div>
        <div className="text-[10px] text-slate-500 font-mono uppercase tracking-wider">
          System Console
        </div>
      </div>

      {/* Terminal Body */}
      <div className="p-4 sm:p-5 h-64 overflow-y-auto font-mono text-sm space-y-1.5 scrollbar-thin scrollbar-thumb-slate-700 scrollbar-track-transparent">
        {logs.length === 0 ? (
          <div className="flex items-center text-slate-500 italic animate-pulse">
            <span className="mr-2 text-indigo-500">▶</span> Waiting for signal
            stream...
          </div>
        ) : (
          logs.map((log, index) => {
            // Highlight percentages for better readability
            const highlightedLog = log.replace(
              /\[(\d+)%\]/g,
              '<span class="text-indigo-400 font-bold">[$1%]</span>',
            );
            return (
              <div
                key={index}
                className="flex hover:bg-slate-800/30 px-1 -mx-1 rounded transition-colors duration-150"
              >
                <span className="text-emerald-500/60 mr-3 select-none">
                  root@cloner:~$
                </span>
                <span
                  className="break-all text-emerald-400 font-medium"
                  dangerouslySetInnerHTML={{ __html: highlightedLog }}
                />
              </div>
            );
          })
        )}
        <div ref={terminalEndRef} />
      </div>
    </div>
  );
};

export default TerminalLogViewer;
