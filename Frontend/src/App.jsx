import { useState, useEffect } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import TerminalLogViewer from "./components/TerminalLogViewer";
import ProgressBar from "./components/ProgressBar";

function App() {
  const [url, setUrl] = useState("");
  const [deepScan, setDeepScan] = useState(false);
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState("");
  const [error, setError] = useState("");
  const [logs, setLogs] = useState([]);
  const [progress, setProgress] = useState(0);
  const [connectionId, setConnectionId] = useState("");

  useEffect(() => {
    const connectSignalR = async () => {
      try {
        const connection = new HubConnectionBuilder()
          .withUrl("http://localhost:5070/hubs/cloneProgress")
          .configureLogging(LogLevel.Information)
          .withAutomaticReconnect()
          .build();

        connection.on("ReceiveLog", (message, percent) => {
          setLogs((prev) => [...prev, message]);
          setProgress(percent);
          if (percent === 100) {
            setStatus("Cloning complete! Downloading zip...");
          } else {
            setStatus("Cloning...");
          }
        });

        await connection.start();
        setConnectionId(connection.connectionId);
        console.log(
          "SignalR Connected. Connection ID:",
          connection.connectionId,
        );
      } catch (e) {
        console.error("SignalR Connection Error: ", e);
      }
    };

    connectSignalR();
  }, []);

  const handleClone = async (e) => {
    e.preventDefault();
    if (!url) return;

    setLoading(true);
    setError("");
    setStatus("Initiating clone process...");
    setLogs([]);
    setProgress(0);

    try {
      const response = await fetch("http://localhost:5070/api/clone", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ url, connectionId, deepScan }),
      });

      if (!response.ok) {
        const errText = await response.text();
        throw new Error(errText || "Failed to clone website.");
      }

      const data = await response.json();
      const downloadFileName = data.fileName || "frontend-clone.zip";
      // Append fileName as query param so backend sets Content-Disposition with the correct name
      const downloadUrl = `http://localhost:5070${data.downloadUrl}?fileName=${encodeURIComponent(downloadFileName)}`;

      setStatus("Downloading archive to your device...");

      const downloadRes = await fetch(downloadUrl);
      if (!downloadRes.ok)
        throw new Error("Failed to download the zip artifact.");

      const blob = await downloadRes.blob();
      const objectUrl = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = objectUrl;
      link.setAttribute("download", "frontend-clone.zip"); // THIS IS MANDATORY
      document.body.appendChild(link);
      link.click();
      link.parentNode.removeChild(link);
      window.URL.revokeObjectURL(objectUrl);

      setStatus("Done!");
    } catch (err) {
      setError(err.message);
      setStatus("");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen w-full bg-[#0B0F19] bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-indigo-900/20 via-[#0B0F19] to-[#0B0F19] flex items-center justify-center p-4 font-sans text-slate-200">
      <div className="max-w-3xl w-full bg-slate-900/80 backdrop-blur-xl rounded-3xl shadow-[0_0_40px_rgba(79,70,229,0.15)] border border-slate-700/50 overflow-hidden relative">
        {/* Glow effect */}
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-3/4 h-1 bg-gradient-to-r from-transparent via-indigo-500 to-transparent opacity-50"></div>

        <div className="p-8 sm:p-10">
          <div className="text-center mb-10">
            <h1 className="text-4xl sm:text-5xl font-black text-transparent bg-clip-text bg-gradient-to-r from-indigo-400 via-purple-400 to-cyan-400 mb-3 tracking-tight">
              FrontendCloner Pro
            </h1>
            <p className="text-slate-400 text-sm sm:text-base font-medium">
              Siêu công cụ clone giao diện web với sức mạnh của Playwright
            </p>
          </div>

          <form onSubmit={handleClone} className="space-y-6">
            <div className="group">
              <label
                htmlFor="url"
                className="block text-sm font-semibold text-slate-300 mb-2 group-focus-within:text-indigo-400 transition-colors"
              >
                Target URL
              </label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                  <svg
                    className="h-5 w-5 text-slate-500 group-focus-within:text-indigo-400 transition-colors"
                    xmlns="http://www.w3.org/2000/svg"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9a9 9 0 019-9"
                    />
                  </svg>
                </div>
                <input
                  id="url"
                  type="url"
                  required
                  placeholder="https://example.com"
                  value={url}
                  onChange={(e) => setUrl(e.target.value)}
                  disabled={loading}
                  className="w-full pl-11 pr-4 py-3.5 bg-slate-950/50 border border-slate-700/70 rounded-xl focus:ring-2 focus:ring-indigo-500/50 focus:border-indigo-500 text-slate-100 placeholder-slate-600 transition-all shadow-inner outline-none disabled:opacity-50 disabled:cursor-not-allowed"
                />
              </div>
            </div>

            <div
              className="flex items-center p-4 bg-slate-800/30 border border-slate-700/50 rounded-xl hover:bg-slate-800/50 transition-colors cursor-pointer group"
              onClick={() => !loading && setDeepScan(!deepScan)}
            >
              <div className="flex items-center h-5">
                <div className="relative flex items-center">
                  <input
                    id="deepScan"
                    type="checkbox"
                    checked={deepScan}
                    onChange={(e) => setDeepScan(e.target.checked)}
                    disabled={loading}
                    className="peer hidden"
                  />
                  <div
                    className={`w-5 h-5 border-2 rounded transition-colors flex items-center justify-center ${deepScan ? "bg-indigo-500 border-indigo-500" : "border-slate-500 bg-transparent"}`}
                  >
                    {deepScan && (
                      <svg
                        className="w-3.5 h-3.5 text-white"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                        strokeWidth={3}
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          d="M5 13l4 4L19 7"
                        />
                      </svg>
                    )}
                  </div>
                </div>
              </div>
              <div className="ml-3">
                <label
                  htmlFor="deepScan"
                  className="font-medium text-slate-300 text-sm cursor-pointer select-none group-hover:text-white transition-colors"
                >
                  Deep Scan Mode
                </label>
                <p className="text-xs text-slate-500 mt-0.5">
                  Bật tự động cuộn trang, tải lazy loading & xử lý nút bấm
                  offline.
                </p>
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full relative overflow-hidden group flex justify-center items-center py-4 px-4 border border-transparent text-base font-bold rounded-xl text-white bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 focus:ring-offset-slate-900 shadow-[0_0_20px_rgba(79,70,229,0.3)] hover:shadow-[0_0_30px_rgba(79,70,229,0.5)] transition-all duration-300 disabled:opacity-70 disabled:cursor-not-allowed transform hover:-translate-y-0.5 active:translate-y-0 disabled:hover:translate-y-0"
            >
              <div className="absolute inset-0 w-full h-full bg-gradient-to-r from-transparent via-white/20 to-transparent -translate-x-full group-hover:animate-[shimmer_1.5s_infinite]"></div>
              {loading ? (
                <div className="flex items-center space-x-2">
                  <svg
                    className="animate-spin h-5 w-5 text-white"
                    xmlns="http://www.w3.org/2000/svg"
                    fill="none"
                    viewBox="0 0 24 24"
                  >
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                    ></circle>
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                    ></path>
                  </svg>
                  <span>Processing...</span>
                </div>
              ) : (
                <span className="flex items-center space-x-2">
                  <svg
                    className="w-5 h-5"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"
                    />
                  </svg>
                  <span>Start Clone</span>
                </span>
              )}
            </button>
          </form>

          {error && (
            <div className="mt-6 p-4 bg-red-950/50 border border-red-500/30 rounded-xl flex items-start space-x-3 backdrop-blur-sm">
              <svg
                className="w-5 h-5 text-red-500 mt-0.5 flex-shrink-0"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                />
              </svg>
              <p className="text-red-400 text-sm">{error}</p>
            </div>
          )}

          {loading && (
            <div className="mt-8 animate-in fade-in slide-in-from-bottom-4 duration-500">
              <ProgressBar progress={progress} />
              <TerminalLogViewer logs={logs} />
            </div>
          )}

          {!loading && status === "Done!" && (
            <div className="mt-8 p-5 bg-emerald-950/40 border border-emerald-500/40 rounded-xl flex items-center justify-center space-x-3 backdrop-blur-sm animate-in zoom-in-95 duration-300">
              <div className="p-2 bg-emerald-500/20 rounded-full">
                <svg
                  className="w-6 h-6 text-emerald-400"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M5 13l4 4L19 7"
                  />
                </svg>
              </div>
              <div>
                <p className="text-emerald-400 font-bold text-lg">Hoàn tất!</p>
                <p className="text-emerald-500/80 text-sm">
                  File zip đã được tải xuống máy của bạn.
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default App;
