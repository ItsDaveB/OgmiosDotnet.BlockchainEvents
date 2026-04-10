import { useEffect, useState, useCallback } from 'react';
import type { BlockchainEvent } from './types';

interface Props {
  event: BlockchainEvent;
  onClose: () => void;
}

function getRuleClass(ruleName: string | undefined): string {
  if (!ruleName) return 'default';
  const lower = ruleName.toLowerCase();
  if (lower.includes('address')) return 'address-match';
  if (lower.includes('metadata') || lower.includes('key')) return 'metadata';
  if (lower.includes('governance') || lower.includes('treasury')) return 'governance';
  return 'default';
}

function CopyableValue({ value, children }: { value: string; children?: React.ReactNode }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = useCallback(() => {
    navigator.clipboard.writeText(value).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  }, [value]);

  return (
    <div className="detail-value copyable" onClick={handleCopy}>
      {children ?? value}
      {copied
        ? <span className="copied-feedback">Copied!</span>
        : <span className="copy-hint">Click to copy</span>}
    </div>
  );
}

/* ─── Section Icons (SVG) ────────────────────── */
const CloudIcon = () => (
  <svg className="section-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M13 8.5a3.5 3.5 0 0 0-6.8-1.2A2.5 2.5 0 0 0 3.5 12h9a2.5 2.5 0 0 0 .5-5" />
  </svg>
);

const BlockIcon = () => (
  <svg className="section-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M8 1L14 4.5V11.5L8 15L2 11.5V4.5L8 1Z" />
    <path d="M8 8L14 4.5" />
    <path d="M8 8L2 4.5" />
    <path d="M8 15V8" />
  </svg>
);

const RuleIcon = () => (
  <svg className="section-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M14 2H2l4.8 5.7V12l2.4 1.2V7.7L14 2Z" />
  </svg>
);

const TxIcon = () => (
  <svg className="section-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M1 8h14M11 4l4 4-4 4" />
  </svg>
);

export function EventDetailDrawer({ event, onClose }: Props) {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [onClose]);

  const data = event.data;
  const tx = data?.transaction;
  const ruleClass = getRuleClass(data?.ruleName);

  return (
    <>
      <div className="drawer-overlay" onClick={onClose} />
      <div className="drawer">
        <div className="drawer-header">
          <div className="drawer-header-left">
            <h3>Event Details</h3>
            {data?.ruleName && (
              <span className={`drawer-event-type cell-rule ${ruleClass}`}>
                {data.ruleName}
              </span>
            )}
          </div>
          <button className="drawer-close" onClick={onClose}>✕</button>
        </div>
        <div className="drawer-body">
          {/* CloudEvent Envelope */}
          <div className="detail-section">
            <div className="detail-section-header">
              <CloudIcon />
              <h4>CloudEvent Envelope</h4>
            </div>
            <div className="detail-row">
              <div className="detail-label">Event ID</div>
              <CopyableValue value={event.id} />
            </div>
            <div className="detail-row">
              <div className="detail-label">Type</div>
              <div className="detail-value">{event.type}</div>
            </div>
            <div className="detail-row">
              <div className="detail-label">Source</div>
              <CopyableValue value={event.source} />
            </div>
            <div className="detail-row">
              <div className="detail-label">Time</div>
              <div className="detail-value">{event.time}</div>
            </div>
            {event.subject && (
              <div className="detail-row">
                <div className="detail-label">Subject</div>
                <div className="detail-value">{event.subject}</div>
              </div>
            )}
          </div>

          {/* Cardano Context */}
          <div className="detail-section">
            <div className="detail-section-header">
              <BlockIcon />
              <h4>Cardano Context</h4>
            </div>
            <div className="detail-row">
              <div className="detail-label">Slot</div>
              <div className="detail-value">{event.cardanoSlot.toLocaleString()}</div>
            </div>
            <div className="detail-row">
              <div className="detail-label">Block Height</div>
              <div className="detail-value">{event.cardanoBlockHeight.toLocaleString()}</div>
            </div>
            <div className="detail-row">
              <div className="detail-label">Block Hash</div>
              <CopyableValue value={event.cardanoBlock} />
            </div>
            <div className="detail-row">
              <div className="detail-label">Era</div>
              <div className="detail-value" style={{ textTransform: 'capitalize' }}>{event.cardanoEra}</div>
            </div>
            <div className="detail-row">
              <div className="detail-label">Network</div>
              <div className="detail-value" style={{ textTransform: 'capitalize' }}>{event.cardanoNetwork}</div>
            </div>
          </div>

          {/* Rule Match */}
          {data && (
            <div className="detail-section">
              <div className="detail-section-header">
                <RuleIcon />
                <h4>Rule Match</h4>
              </div>
              <div className="detail-row">
                <div className="detail-label">Rule Name</div>
                <div className="detail-value">{data.ruleName}</div>
              </div>
              <div className="detail-row">
                <div className="detail-label">Rule ID</div>
                <div className="detail-value">{data.ruleId}</div>
              </div>
              <div className="detail-row">
                <div className="detail-label">Transaction</div>
                <CopyableValue value={data.transactionId} />
              </div>
              {Object.keys(data.matchedCriteria).length > 0 && (
                <>
                  <div className="detail-row" style={{ marginTop: 4 }}>
                    <div className="detail-label">Matched Criteria</div>
                  </div>
                  <div className="criteria-list">
                    {Object.entries(data.matchedCriteria).map(([key, val]) => (
                      <div key={key} className="criteria-item">
                        <span className="criteria-key">{key}:</span>
                        <span className="criteria-val">{typeof val === 'object' ? JSON.stringify(val) : String(val)}</span>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Transaction */}
          {tx && (
            <div className="detail-section">
              <div className="detail-section-header">
                <TxIcon />
                <h4>Transaction</h4>
              </div>
              <div className="detail-row">
                <div className="detail-label">Fee</div>
                <div className="detail-value">
                  <div className="fee-display">
                    <span className="fee-ada">₳ {(tx.fee / 1_000_000).toFixed(6)}</span>
                    <span className="fee-lovelace">{tx.fee.toLocaleString()} lovelace</span>
                  </div>
                </div>
              </div>
              {tx.inputAddresses.length > 0 && (
                <div className="detail-row">
                  <div className="detail-label">Inputs ({tx.inputAddresses.length})</div>
                  <div className="detail-value">
                    <div className="address-list">
                      {tx.inputAddresses.map((addr, i) => (
                        <div
                          key={i}
                          className="address-item"
                          title="Click to copy"
                          onClick={() => navigator.clipboard.writeText(addr)}
                        >
                          {addr}
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              )}
              {tx.outputAddresses.length > 0 && (
                <div className="detail-row">
                  <div className="detail-label">Outputs ({tx.outputAddresses.length})</div>
                  <div className="detail-value">
                    <div className="address-list">
                      {tx.outputAddresses.map((addr, i) => (
                        <div
                          key={i}
                          className="address-item"
                          title="Click to copy"
                          onClick={() => navigator.clipboard.writeText(addr)}
                        >
                          {addr}
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </>
  );
}
