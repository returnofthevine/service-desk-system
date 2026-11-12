import { useState } from 'react'
import { FileSpreadsheet, Upload } from 'lucide-react'
import { writeApi } from '../api'
import { Notice } from '../components/Feedback'

export default function AssetsImportPage() {
  const [file, setFile] = useState(null)
  const [pending, setPending] = useState(false)
  const [result, setResult] = useState(null)

  async function upload(event) {
    event.preventDefault()
    if (!file) return
    setPending(true)
    setResult(null)
    const body = new FormData()
    body.append('file', file)

    try {
      const data = await writeApi('/api/assets/import', { body })
      setResult({ kind: 'success', data })
    } catch (error) {
      setResult({ kind: 'error', message: error.message, rows: error.body?.errors })
    } finally {
      setPending(false)
    }
  }

  return (
    <>
      <div className="page-heading">
        <h1>Import assets</h1>
        <p>Add workstation records from a CSV file.</p>
      </div>

      <section className="panel detail-panel import-panel">
        <div className="section-heading">
          <span className="section-icon"><FileSpreadsheet size={19} aria-hidden="true" /></span>
          <h2>Upload a CSV</h2>
        </div>
        <form className="field-stack" onSubmit={upload}>
          <div>
            <label htmlFor="asset-file">Asset file</label>
            <p id="file-limit" className="field-hint">Up to 1 MiB and 1,000 data records.</p>
            <input
              id="asset-file"
              type="file"
              accept=".csv,text/csv"
              aria-describedby="file-limit file-format"
              required
              disabled={pending}
              onChange={(event) => {
                setFile(event.target.files[0] ?? null)
                setResult(null)
              }}
            />
          </div>
          <div className="file-format" id="file-format">
            <p className="field-hint">Required header in column order</p>
            <code>assetTag,hostname,bitLockerState,tpmState,lastCheckedAtUtc</code>
          </div>
          <button className="button button-primary" type="submit" disabled={pending || !file}>
            <Upload size={17} aria-hidden="true" /> {pending ? 'Importing…' : 'Import CSV'}
          </button>
        </form>
      </section>

      {result?.kind === 'error' && (
        <div className="import-result">
          <Notice error>{result.message}</Notice>
          {Array.isArray(result.rows) && result.rows.length > 0 && (
            <div className="table-scroll panel" role="region" aria-label="CSV validation errors" tabIndex={0}>
              <table className="error-table">
                <thead><tr><th scope="col">Row</th><th scope="col">Field</th><th scope="col">Message</th></tr></thead>
                <tbody>
                  {result.rows.map(({ row, field, message }) => (
                    <tr key={`${row}:${field}:${message}`}><td>{row}</td><td>{field}</td><td>{message}</td></tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {result?.kind === 'success' && (
        <section className="panel detail-panel import-result" aria-label="Imported assets">
          <Notice>{result.data.createdCount} {result.data.createdCount === 1 ? 'asset' : 'assets'} created.</Notice>
          <ul className="imported-tags">
            {result.data.assets.map((asset) => <li className="asset-tag" key={asset.id}>{asset.assetTag}</li>)}
          </ul>
        </section>
      )}
    </>
  )
}
