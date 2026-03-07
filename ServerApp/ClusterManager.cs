using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Serilog.Sinks.File;
using Serilog;

namespace ServerApp;

public class ClusterManager {
    private readonly ConcurrentDictionary<string, DateTime> m_lastSeen = new();
    private readonly ConcurrentDictionary<string, bool> m_isActive = new();
    private readonly ConcurrentDictionary<string, string> m_pendingCommands = new();

    public void StartWatchdog() {
        _ = Task.Run(async () => {
            while(true) {
                await Task.Delay(5000);
                var now = DateTime.UtcNow;

                foreach(var sensor in m_lastSeen) {
                    string id = sensor.Key;
                    DateTime lastSeen = sensor.Value;

                    if(m_isActive.TryGetValue(id, out bool isActive) && isActive) {
                        if((now - lastSeen).TotalSeconds > 30) {
                            Log.Information("Sensor with id {SensorId} is DEAD!!!", id);
                            m_isActive[id] = false;

                            var standbySensor = m_isActive.FirstOrDefault(x => x.Value == false).Key;
                            if(standbySensor != null) {
                                Log.Information("Promoting Standby sensor with id {SensorId} to ACTIVE!!!", standbySensor);
                                m_pendingCommands[standbySensor] = "BECOME_ACIVE";
                                m_isActive[standbySensor] = true;
                            }
                        }
                    }
                }
            }
        });
    }

    public void RecordHeartbeat(string sensorId, bool isHeartbeatMessage) {
        m_lastSeen[sensorId] = DateTime.UtcNow;

        if(!m_isActive.ContainsKey(sensorId)) {
            m_isActive[sensorId] = !isHeartbeatMessage;
        }
    }

    public string? GetPendingCommand(string sensorId) {
        if(m_pendingCommands.TryRemove(sensorId, out string? command)) {
            return command;
        }
        return null;
    }

    public bool IsZombie(string sensorId, bool isHeartbeatMessage) {
        return !isHeartbeatMessage && m_isActive.TryGetValue(sensorId, out bool active) && !active;
    }
}