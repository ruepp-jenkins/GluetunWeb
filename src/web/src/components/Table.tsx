import type { ReactNode, ThHTMLAttributes, TdHTMLAttributes } from 'react'

/** High-density monospaced table primitives with horizontal-scroll safety. */
export function Table({ head, children }: { head: ReactNode; children: ReactNode }) {
  return (
    <div className="overflow-x-auto border border-line">
      <table className="w-full border-collapse text-[12px]">
        <thead className="bg-panel-2">
          <tr>{head}</tr>
        </thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  )
}

export function Th({ children, className = '', ...rest }: ThHTMLAttributes<HTMLTableCellElement>) {
  return (
    <th
      className={`border-b border-line px-3 py-1.5 text-left text-[10px] font-normal uppercase tracking-widest text-muted ${className}`}
      {...rest}
    >
      {children}
    </th>
  )
}

export function Td({ children, className = '', ...rest }: TdHTMLAttributes<HTMLTableCellElement>) {
  return (
    <td className={`border-b border-line/60 px-3 py-1.5 align-middle ${className}`} {...rest}>
      {children}
    </td>
  )
}

export function Tr({ children, onClick }: { children: ReactNode; onClick?: () => void }) {
  return (
    <tr
      onClick={onClick}
      className={`hover:bg-panel-2/60 ${onClick ? 'cursor-pointer' : ''}`}
    >
      {children}
    </tr>
  )
}
