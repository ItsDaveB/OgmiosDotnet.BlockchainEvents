export interface BlockchainEvent {
  id: string;
  type: string;
  source: string;
  time: string;
  subject: string | null;
  cardanoSlot: number;
  cardanoBlockHeight: number;
  cardanoBlock: string;
  cardanoEra: string;
  cardanoNetwork: string;
  data: {
    transactionId: string;
    slot: number;
    blockHeight: number;
    blockHash: string;
    ruleId: string;
    ruleName: string;
    matchedCriteria: Record<string, string>;
    transaction: {
      id: string;
      fee: number;
      inputAddresses: string[];
      outputAddresses: string[];
    } | null;
  } | null;
}
