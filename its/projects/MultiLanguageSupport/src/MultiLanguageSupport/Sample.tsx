import React, { useContext, createContext } from 'react'; // typescript:S1128

interface Props {
  children: any;
}

const ExampleContext = createContext<{} | undefined>(undefined);

export const ExampleProvider: React.FC<Props> = (props) => {
  const value = {}; // typescript:S6481
  return <ExampleContext.Provider value={value}>{props.children}</ExampleContext.Provider>;
};