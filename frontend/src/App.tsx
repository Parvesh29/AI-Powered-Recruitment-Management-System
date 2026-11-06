import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import axios from 'axios'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import {
  Alert, AppBar, Box, Button, Card, CardContent, Chip, CircularProgress, Container, Dialog,
  DialogActions, DialogContent, DialogTitle, FormControl, FormHelperText, InputLabel, MenuItem, Paper, Select, Snackbar, Stack, TextField, Toolbar, Typography,
} from '@mui/material'
import { BrowserRouter, Link, Navigate, Route, Routes, useNavigate } from 'react-router'
import './App.css'

type Session = { accessToken: string; user: { displayName: string; email: string; organizationId: string; roles: string[] } }
type Job = { id: string; title: string; description: string; location?: string; employmentType?: string; minimumExperienceYears: number; maximumExperienceYears?: number; status: number; departmentId: string; departmentName: string; departmentIds?: string[]; departmentNames?: string[]; publishedAt?: string }
type PublicJob = { id: string; organizationName: string; organizationSlug: string; title: string; description: string; location?: string; employmentType?: string; minimumExperienceYears: number; maximumExperienceYears?: number; publishedAt?: string }
type Department = { id: string; name: string }
type Page<T> = { items: T[]; total: number; page: number; pageSize: number }
const api = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:8080', withCredentials: true })

function App() {
  const [session, setSession] = useState<Session | null>(null)
  const [checking, setChecking] = useState(true)
  useEffect(() => {
    api.post('/api/auth/refresh').then(({ data }) => setSession(data?.accessToken ? data : null)).catch(() => setSession(null)).finally(() => setChecking(false))
  }, [])
  if (checking) return <Box className="loading"><CircularProgress size={28} /><Typography>Loading HireFlow…</Typography></Box>
  return <BrowserRouter><Routes>
    <Route path="/" element={<PublicJobs session={session} onLogout={() => setSession(null)} />} />
    <Route path="/login" element={session ? <Navigate to="/manage/jobs" replace /> : <Login onLogin={setSession} />} />
    <Route path="/register" element={session ? <Navigate to="/manage/jobs" replace /> : <Register onRegister={setSession} />} />
    <Route path="/manage/jobs" element={session ? <JobWorkspace session={session} onLogout={() => setSession(null)} /> : <Navigate to="/login" replace />} />
    <Route path="/manage/candidates" element={session ? <CandidateWorkspace session={session} onLogout={() => setSession(null)} /> : <Navigate to="/login" replace />} />
    <Route path="/manage/pipeline" element={session ? <PipelineWorkspace session={session} onLogout={() => setSession(null)} /> : <Navigate to="/login" replace />} />
    <Route path="/manage/analytics" element={session ? <AnalyticsWorkspace session={session} onLogout={() => setSession(null)} /> : <Navigate to="/login" replace />} />
    <Route path="/manage/assistant" element={session ? <AssistantWorkspace session={session} onLogout={() => setSession(null)} /> : <Navigate to="/login" replace />} />
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes></BrowserRouter>
}

function formatExperience(years: number): string {
  const totalMonths = Math.round(years * 12)
  const wholeYears = Math.floor(totalMonths / 12)
  const remainingMonths = totalMonths % 12
  return [wholeYears ? `${wholeYears} ${wholeYears === 1 ? 'year' : 'years'}` : '', remainingMonths ? `${remainingMonths} ${remainingMonths === 1 ? 'month' : 'months'}` : '']
    .filter(Boolean).join(' ') || '0 months'
}

function formatAssistantResult(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(formatAssistantResult)
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([key, item]) => {
      if ((key === 'totalExperienceYears' || key === 'minimumExperienceYears') && typeof item === 'number')
        return [key.replace('Years', ''), formatExperience(item)]
      return [key, formatAssistantResult(item)]
    }))
  }
  return value
}

function Header({ session, onLogout }: { session?: Session | null; onLogout?: () => void }) {
  const navigate = useNavigate()
  const [liveNotification, setLiveNotification] = useState('')
  const accessToken = session?.accessToken
  useEffect(() => {
    if (!accessToken) return
    const connection = new HubConnectionBuilder().withUrl(`${import.meta.env.VITE_API_URL ?? 'http://localhost:8080'}/hubs/notifications`, { accessTokenFactory: () => accessToken }).withAutomaticReconnect().configureLogging(LogLevel.Warning).build()
    connection.on('notification', (item: { title?: string; message?: string }) => setLiveNotification([item.title, item.message].filter(Boolean).join(': ')))
    void connection.start().catch(() => undefined)
    return () => { void connection.stop() }
  }, [accessToken])
  const signOut = async () => { try { await api.post('/api/auth/logout') } finally { onLogout?.(); navigate('/') } }
  return <><AppBar position="sticky" elevation={0} className="app-bar"><Toolbar className="toolbar">
    <Link to="/" className="brand"><span className="brand-mark">H</span>HireFlow <span className="brand-ai">AI</span></Link>
    <Box sx={{ flex: 1 }} />
    {session ? <><Typography className="user-label">{session.user.displayName}</Typography><Button color="inherit" onClick={() => navigate('/manage/jobs')}>Jobs</Button><Button color="inherit" onClick={() => navigate('/manage/candidates')}>Candidates</Button><Button color="inherit" onClick={() => navigate('/manage/pipeline')}>Pipeline</Button><Button color="inherit" onClick={() => navigate('/manage/analytics')}>Analytics</Button><Button color="inherit" onClick={() => navigate('/manage/assistant')}>Assistant</Button><Button variant="outlined" color="inherit" onClick={signOut}>Sign out</Button></> : <><Button variant="text" onClick={() => navigate('/register')}>Create workspace</Button><Button variant="contained" onClick={() => navigate('/login')}>Recruiter sign in</Button></>}
  </Toolbar></AppBar><Snackbar open={Boolean(liveNotification)} autoHideDuration={6000} onClose={() => setLiveNotification('')} message={liveNotification} /></>
}

function PublicJobs({ session, onLogout }: { session: Session | null; onLogout: () => void }) {
  const [search, setSearch] = useState('')
  const [result, setResult] = useState<Page<PublicJob> | null>(null)
  const [error, setError] = useState('')
  const [applyJob, setApplyJob] = useState<PublicJob | null>(null)
  const [applyStatus, setApplyStatus] = useState('')
  const [applyBusy, setApplyBusy] = useState(false)
  const [applyResumeName, setApplyResumeName] = useState('')
  useEffect(() => { api.get('/api/jobs/public', { params: { search, pageSize: 30 } }).then(({ data }) => { setResult(data); setError('') }).catch(() => setError('Could not load open roles. Check that the API is running.')) }, [search])
  const apply = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); if (!applyJob) return
    const form = new FormData(event.currentTarget)
    const selectedResume = form.get('resume')
    if (!(selectedResume instanceof File) || selectedResume.size === 0) { setApplyStatus('Please upload your resume to submit your application.'); return }
    setApplyBusy(true); setApplyStatus('')
    try {
      const payload = new FormData()
      for (const name of ['firstName', 'lastName', 'email', 'phoneNumber']) {
        const value = form.get(name)
        if (value) payload.append(name, String(value))
      }
      payload.append('resume', selectedResume)
      await api.post(`/api/jobs/public/${applyJob.id}/applications`, payload)
      setApplyJob(null); setApplyResumeName(''); setApplyStatus('Application submitted. The hiring team will review your profile.')
    }
    catch (e) { setApplyStatus(axios.isAxiosError(e) ? e.response?.data?.message ?? 'This application could not be submitted.' : 'This application could not be submitted.') }
    finally { setApplyBusy(false) }
  }
  return <><Header session={session} onLogout={onLogout} /><Container maxWidth="lg" className="page-content">
    <Box className="hero-panel"><div><Typography className="eyebrow">A BETTER WAY TO BUILD YOUR TEAM</Typography><Typography variant="h1">Find the work that moves you.</Typography><Typography className="hero-copy">Explore open roles from teams using HireFlow AI to make hiring more thoughtful.</Typography></div><div className="hero-orbit"><span>HF</span><i /><b /></div></Box>
    <Stack direction={{ xs: 'column', sm: 'row' }} sx={{ justifyContent: 'space-between', alignItems: { sm: 'end' }, gap: 2 }} className="listing-heading"><div><Typography className="eyebrow">CAREER OPPORTUNITIES</Typography><Typography variant="h2">Open positions</Typography></div><TextField size="small" label="Search roles" value={search} onChange={e => setSearch(e.target.value)} sx={{ width: { xs: '100%', sm: 310 } }} /></Stack>
    {error && <Alert severity="error">{error}</Alert>}
    {!result && !error && <CircularProgress />}
    {result && result.items.length === 0 && <Paper className="empty-state"><Typography variant="h6">No open roles yet</Typography><Typography color="text.secondary">New opportunities will appear here as teams post them.</Typography></Paper>}
    <Box className="job-grid">{result?.items.map(job => <Card className="job-card" key={job.id} elevation={0}><CardContent>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}><Chip label={job.organizationName} size="small" className="company-chip" /><Typography variant="caption" color="text.secondary">{job.publishedAt ? new Date(job.publishedAt).toLocaleDateString() : ''}</Typography></Stack>
      <Typography variant="h5" className="job-title">{job.title}</Typography><Typography color="text.secondary" className="job-company">{[job.location, job.employmentType].filter(Boolean).join(' · ') || 'Location flexible'}</Typography>
      <Typography className="job-description">{job.description}</Typography><Stack direction="row" sx={{ mt: 2, gap: 1, flexWrap: 'wrap' }}><Chip size="small" variant="outlined" label={`${job.minimumExperienceYears}${job.maximumExperienceYears ? `–${job.maximumExperienceYears}` : '+'} years`} /><Typography variant="caption" color="text.secondary">Applications open soon</Typography></Stack>
      <Button size="small" variant="contained" sx={{ mt: 2 }} onClick={() => { setApplyJob(job); setApplyStatus(''); setApplyResumeName('') }}>Apply now</Button>
    </CardContent></Card>)}</Box>
    {applyStatus && !applyJob && <Alert severity={applyStatus.startsWith('Application submitted') ? 'success' : 'error'} sx={{ mt: 2 }}>{applyStatus}</Alert>}
    <Dialog open={Boolean(applyJob)} onClose={() => setApplyJob(null)} fullWidth maxWidth="sm"><Box component="form" onSubmit={apply}><DialogTitle>Apply{applyJob ? ` · ${applyJob.title}` : ''}</DialogTitle><DialogContent className="form-stack"><Typography color="text.secondary">Share your contact details with the hiring team.</Typography>{applyStatus && <Alert severity="error">{applyStatus}</Alert>}<TextField name="firstName" label="First name" required slotProps={{ htmlInput: { maxLength: 100 } }} /><TextField name="lastName" label="Last name" slotProps={{ htmlInput: { maxLength: 100 } }} /><TextField name="email" label="Email" type="email" required /><TextField name="phoneNumber" label="Phone" /><Button component="label" variant="outlined" sx={{ alignSelf: 'flex-start' }}>Upload resume *<input name="resume" type="file" accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" hidden onChange={event => { setApplyResumeName(event.target.files?.[0]?.name ?? ''); setApplyStatus('') }} /></Button><Typography variant="caption" color="text.secondary">Required. PDF or DOCX, up to 10 MB{applyResumeName ? ` · ${applyResumeName}` : ''}</Typography></DialogContent><DialogActions><Button onClick={() => setApplyJob(null)}>Cancel</Button><Button type="submit" variant="contained" disabled={applyBusy}>{applyBusy ? 'Submitting…' : 'Submit application'}</Button></DialogActions></Box></Dialog>
    <footer className="app-footer">HireFlow AI <span>·</span> Recruitment, thoughtfully connected.</footer>
  </Container></>
}

function Login({ onLogin }: { onLogin: (session: Session) => void }) {
  const navigate = useNavigate(); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setBusy(true); setError('')
    const form = new FormData(event.currentTarget)
    try { const { data } = await api.post('/api/auth/login', { organizationSlug: form.get('organizationSlug'), email: form.get('email'), password: form.get('password') }); onLogin(data); navigate('/manage/jobs') }
    catch { setError('Sign in failed. Check your organization slug, email, and password.') } finally { setBusy(false) }
  }
  return <><Header /><Container maxWidth="sm" className="login-wrap"><Paper className="login-card" elevation={0}><Typography className="eyebrow">RECRUITER WORKSPACE</Typography><Typography variant="h2">Welcome back</Typography><Typography color="text.secondary" sx={{ mb: 3 }}>Sign in to manage your hiring pipeline.</Typography>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}<Box component="form" onSubmit={submit} className="form-stack"><TextField name="organizationSlug" label="Organization slug" required autoComplete="organization" /><TextField name="email" label="Work email" type="email" required autoComplete="username" /><TextField name="password" label="Password" type="password" required autoComplete="current-password" /><Button type="submit" size="large" variant="contained" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</Button></Box>
    <Typography color="text.secondary" sx={{ mt: 2, textAlign: 'center' }}>New to HireFlow? <Link to="/register">Create an organization</Link></Typography>
  </Paper></Container></>
}

function Register({ onRegister }: { onRegister: (session: Session) => void }) {
  const navigate = useNavigate(); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setBusy(true); setError('')
    const form = new FormData(event.currentTarget)
    const payload = { organizationName: form.get('organizationName'), organizationSlug: String(form.get('organizationSlug') ?? '').trim().toLowerCase().replace(/\s+/g, '-'), firstName: form.get('firstName'), lastName: form.get('lastName'), email: form.get('email'), password: form.get('password') }
    try { const { data } = await api.post('/api/auth/register', payload); onRegister(data); navigate('/manage/jobs') }
    catch (e) { setError(axios.isAxiosError(e) ? e.response?.data?.detail ?? e.response?.data?.title ?? 'Could not create your organization. Check the organization slug and password requirements.' : 'Could not create your organization.') }
    finally { setBusy(false) }
  }
  return <><Header /><Container maxWidth="sm" className="login-wrap"><Paper className="login-card" elevation={0}><Typography className="eyebrow">GET STARTED</Typography><Typography variant="h2">Create your workspace</Typography><Typography color="text.secondary" sx={{ mb: 3 }}>Set up your organization and the first administrator account.</Typography>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    <Box component="form" onSubmit={submit} className="form-stack"><TextField name="organizationName" label="Organization name" required autoComplete="organization" slotProps={{ htmlInput: { maxLength: 200 } }} /><TextField name="organizationSlug" label="Organization slug" required helperText="Use lowercase letters, numbers, and hyphens." slotProps={{ htmlInput: { maxLength: 120, pattern: '[a-z0-9]+(?:-[a-z0-9]+)*' } }} /><Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}><TextField name="firstName" label="First name" required fullWidth autoComplete="given-name" /><TextField name="lastName" label="Last name" required fullWidth autoComplete="family-name" /></Stack><TextField name="email" label="Work email" type="email" required autoComplete="email" /><TextField name="password" label="Password" type="password" required autoComplete="new-password" helperText="At least 12 characters with lowercase, uppercase, and a number." slotProps={{ htmlInput: { minLength: 12 } }} /><Button type="submit" size="large" variant="contained" disabled={busy}>{busy ? 'Creating workspace…' : 'Create account'}</Button></Box>
    <Typography color="text.secondary" sx={{ mt: 2, textAlign: 'center' }}>Already have an account? <Link to="/login">Sign in</Link></Typography>
  </Paper></Container></>
}

function JobWorkspace({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [jobs, setJobs] = useState<Job[]>([]); const [departments, setDepartments] = useState<Department[]>([])
  const [selectedDepartmentIds, setSelectedDepartmentIds] = useState<string[]>([])
  const [open, setOpen] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [status, setStatus] = useState('')
  const [matchingOpen, setMatchingOpen] = useState(false); const [matchingJob, setMatchingJob] = useState<Job | null>(null)
  const [matchingCandidates, setMatchingCandidates] = useState<Candidate[]>([]); const [matchingCandidateId, setMatchingCandidateId] = useState('')
  const [matchingResult, setMatchingResult] = useState<MatchingResult | null>(null); const [matchingBusy, setMatchingBusy] = useState(false)
  const auth = useMemo(() => ({ Authorization: `Bearer ${session.accessToken}` }), [session.accessToken])
  const load = useCallback(async () => {
    try { const [jobResponse, departmentResponse] = await Promise.all([api.get<Page<Job>>('/api/jobs', { headers: auth, params: { page: 1, pageSize: 100, sortBy: 'createdAt', descending: true } }), api.get<Department[]>('/api/jobs/departments', { headers: auth })]); setJobs(jobResponse.data.items); setDepartments(departmentResponse.data); setError('') }
    catch { setError('Could not load jobs. Your account may not have job permissions.') }
  }, [auth])
  useEffect(() => { void load() }, [load])
  const openMatching = async (job: Job) => {
    setMatchingJob(job); setMatchingOpen(true); setMatchingCandidateId(''); setMatchingResult(null); setError('')
    try { const { data } = await api.get<Page<Candidate>>('/api/candidates', { headers: auth, params: { page: 1, pageSize: 100 } }); setMatchingCandidates(data.items); setMatchingCandidateId(data.items[0]?.id ?? '') }
    catch { setError('Could not load candidates for matching. Check candidate permissions.') }
  }
  const calculateMatch = async () => {
    if (!matchingJob || !matchingCandidateId) return
    setMatchingBusy(true); setError('')
    try { const { data } = await api.post<MatchingResult>(`/api/jobs/${matchingJob.id}/matches/${matchingCandidateId}`, {}, { headers: auth }); setMatchingResult(data) }
    catch { setError('Could not calculate the match. Verify the job and candidate belong to your organization.') }
    finally { setMatchingBusy(false) }
  }
  const createJob = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setBusy(true); setError(''); setStatus('')
    const form = new FormData(event.currentTarget); const max = String(form.get('maximumExperienceYears') ?? '').trim()
    if (selectedDepartmentIds.length === 0) { setError('Select at least one department.'); setBusy(false); return }
    const payload = { title: form.get('title'), description: form.get('description'), location: form.get('location'), employmentType: form.get('employmentType'), minimumExperienceYears: Number(form.get('minimumExperienceYears') || 0), maximumExperienceYears: max ? Number(max) : null, departmentId: selectedDepartmentIds[0], departmentIds: selectedDepartmentIds, status: Number(form.get('status')), skillNames: String(form.get('skillNames') ?? '').split(',').map(x => x.trim()).filter(Boolean), recruiterIds: [] }
    try { await api.post('/api/jobs', payload, { headers: auth }); setOpen(false); setStatus('Job created.'); await load() }
    catch (e) { setError(axios.isAxiosError(e) ? e.response?.data?.detail ?? e.response?.data ?? 'Could not create the job.' : 'Could not create the job.') }
    finally { setBusy(false) }
  }
  const changeStatus = async (jobId: string, next: number) => {
    try { const { data: job } = await api.get(`/api/jobs/${jobId}`, { headers: auth }); const departmentIds = job.departments?.map((d: Department) => d.id) ?? job.departmentIds ?? [job.departmentId]; await api.put(`/api/jobs/${jobId}`, { title: job.title, description: job.description, location: job.location, employmentType: job.employmentType, minimumExperienceYears: job.minimumExperienceYears, maximumExperienceYears: job.maximumExperienceYears, departmentId: departmentIds[0], departmentIds, status: next, skillNames: job.skills.map((x: { name: string }) => x.name), recruiterIds: job.recruiters.map((x: { id: string }) => x.id) }, { headers: auth }); await load() }
    catch { setError('Could not update job status.') }
  }
  return <><Header session={session} onLogout={onLogout} /><Container maxWidth="lg" className="workspace-content">
    <Stack direction={{ xs: 'column', sm: 'row' }} sx={{ justifyContent: 'space-between', alignItems: { sm: 'end' }, gap: 2 }} className="workspace-heading"><div><Typography className="eyebrow">HIRING OPERATIONS</Typography><Typography variant="h2">Job openings</Typography><Typography color="text.secondary">Create and manage roles for your organization.</Typography></div><Button variant="contained" onClick={() => { setSelectedDepartmentIds([]); setOpen(true) }}>Create job</Button></Stack>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}{status && <Alert severity="success" sx={{ mb: 2 }}>{status}</Alert>}
    <Box className="job-grid">{jobs.map(job => <Card className="job-card" key={job.id} elevation={0}><CardContent>
      <Stack direction="row" sx={{ justifyContent: 'space-between' }}><Chip size="small" label={['Draft', 'Open', 'On hold', 'Closed'][job.status] ?? 'Unknown'} color={job.status === 1 ? 'success' : 'default'} /><Typography variant="caption" color="text.secondary">{job.departmentNames?.join(', ') || job.departmentName}</Typography></Stack>
      <Typography variant="h5" className="job-title">{job.title}</Typography><Typography color="text.secondary">{[job.location, job.employmentType].filter(Boolean).join(' · ') || 'Location flexible'}</Typography>
      <Typography className="job-description">{job.description}</Typography><Stack direction="row" sx={{ mt: 2, gap: 1, flexWrap: 'wrap' }}><Button size="small" onClick={() => void openMatching(job)}>Match candidates</Button>
        {job.status !== 1 && job.status !== 3 && <Button size="small" onClick={() => void changeStatus(job.id, 1)}>Publish</Button>}
        {job.status === 1 && <Button size="small" color="warning" onClick={() => void changeStatus(job.id, 2)}>Put on hold</Button>}
        {job.status !== 3 && <Button size="small" color="error" onClick={() => void changeStatus(job.id, 3)}>Close</Button>}
      </Stack>
    </CardContent></Card>)}</Box>
    {jobs.length === 0 && !error && <Paper className="empty-state"><Typography variant="h6">No jobs yet</Typography><Typography color="text.secondary">Create your first opening to start building your hiring pipeline.</Typography></Paper>}
    <Dialog open={matchingOpen} onClose={() => setMatchingOpen(false)} fullWidth maxWidth="sm"><DialogTitle>Candidate match{matchingJob ? ` · ${matchingJob.title}` : ''}</DialogTitle><DialogContent className="form-stack">
      {matchingCandidates.length === 0 ? <Typography color="text.secondary">Create candidate profiles before calculating a match.</Typography> : <TextField label="Candidate" select value={matchingCandidateId} onChange={e => { setMatchingCandidateId(e.target.value); setMatchingResult(null) }}>{matchingCandidates.map(candidate => <MenuItem key={candidate.id} value={candidate.id}>{[candidate.firstName, candidate.lastName].filter(Boolean).join(' ') || candidate.email || 'Unnamed candidate'}</MenuItem>)}</TextField>}
      {matchingResult && <Paper className="empty-state" sx={{ textAlign: 'left' }}><Typography variant="h4" sx={{ color: '#17735e', fontWeight: 700 }}>{matchingResult.score}% match</Typography><Typography variant="caption" color="text.secondary">{matchingResult.algorithmVersion} · scores assist review and do not make hiring decisions.</Typography><Stack direction="row" sx={{ gap: 1, flexWrap: 'wrap', mt: 2 }}>{matchingResult.matchedSkills.map(skill => <Chip key={skill} size="small" color="success" label={skill} />)}{matchingResult.missingRequiredSkills.map(skill => <Chip key={skill} size="small" color="error" variant="outlined" label={`Missing ${skill}`} />)}</Stack><Box sx={{ mt: 2 }}>{matchingResult.categories.map(category => <Stack key={category.label} direction="row" sx={{ justifyContent: 'space-between', borderTop: '1px solid #e6ece8', py: 1 }}><Typography variant="body2">{category.label} ({Math.round(category.weight * 100)}%)</Typography><Typography variant="body2" sx={{ fontWeight: 650 }}>{category.score}%</Typography></Stack>)}</Box>{matchingResult.warnings.length > 0 && <Typography variant="caption" color="text.secondary">Data notes: {matchingResult.warnings.join(', ')}</Typography>}</Paper>}
    </DialogContent><DialogActions><Button onClick={() => setMatchingOpen(false)}>Close</Button><Button variant="contained" onClick={() => void calculateMatch()} disabled={!matchingCandidateId || matchingBusy}>{matchingBusy ? 'Calculating…' : 'Calculate match'}</Button></DialogActions></Dialog>
    <Dialog open={open} onClose={() => setOpen(false)} fullWidth maxWidth="sm"><Box component="form" onSubmit={createJob}><DialogTitle>Create a job opening</DialogTitle><DialogContent className="form-stack">
      <TextField name="title" label="Job title" required slotProps={{ htmlInput: { maxLength: 200 } }} />
      <FormControl required disabled={departments.length === 0}><InputLabel id="job-departments-label">Departments</InputLabel><Select labelId="job-departments-label" multiple value={selectedDepartmentIds} label="Departments" onChange={event => setSelectedDepartmentIds(typeof event.target.value === 'string' ? event.target.value.split(',') : event.target.value)} renderValue={selected => selected.map(id => departments.find(d => d.id === id)?.name ?? id).join(', ')}>{departments.map(d => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}</Select><FormHelperText>{departments.length === 0 ? 'Loading departments…' : 'Select one or more departments.'}</FormHelperText></FormControl>
      <Stack direction={{ xs: 'column', sm: 'row' }} sx={{ gap: 2 }}><TextField name="location" label="Location" fullWidth /><TextField name="employmentType" label="Employment type" fullWidth placeholder="Full-time" /></Stack>
      <Stack direction="row" sx={{ gap: 2 }}><TextField name="minimumExperienceYears" label="Min. experience" type="number" defaultValue={0} slotProps={{ htmlInput: { min: 0 } }} fullWidth /><TextField name="maximumExperienceYears" label="Max. experience" type="number" slotProps={{ htmlInput: { min: 0 } }} fullWidth /></Stack>
      <TextField name="skillNames" label="Required skills" placeholder="React, TypeScript, SQL" helperText="Separate skills with commas" />
      <TextField name="status" label="Initial status" select defaultValue={0}><MenuItem value={0}>Draft</MenuItem><MenuItem value={1}>Open and publish</MenuItem><MenuItem value={2}>On hold</MenuItem></TextField>
      <TextField name="description" label="Description" required multiline minRows={5} slotProps={{ htmlInput: { maxLength: 20000 } }} />
    </DialogContent><DialogActions><Button onClick={() => setOpen(false)}>Cancel</Button><Button type="submit" variant="contained" disabled={busy}>{busy ? 'Saving…' : 'Save job'}</Button></DialogActions></Box></Dialog>
    <footer className="app-footer">HireFlow AI <span>·</span> Recruitment, thoughtfully connected.</footer>
  </Container></>
}

function AssistantWorkspace({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [task, setTask] = useState('find_candidates'); const [question, setQuestion] = useState('Find candidates with 3 years of .NET experience'); const [result, setResult] = useState<unknown>(null); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  const headers = useMemo(() => ({ Authorization: `Bearer ${session.accessToken}` }), [session.accessToken])
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); setBusy(true); setError(''); setResult(null); const form = new FormData(event.currentTarget); const payload = { task, question, candidateId: form.get('candidateId') || null, jobId: form.get('jobId') || null, jobTitle: form.get('jobTitle') || null, skills: String(form.get('skills') || '').split(',').map(x => x.trim()).filter(Boolean) }; try { const { data } = await api.post('/api/assistant/query', payload, { headers }); setResult(data) } catch (e) { setError(axios.isAxiosError(e) ? e.response?.data?.message ?? e.response?.data?.detail ?? 'The assistant request failed.' : 'The assistant request failed.') } finally { setBusy(false) } }
  const questionDefaults: Record<string, string> = { find_candidates: 'Find candidates with 3 years of .NET experience', summarize_candidate: 'Summarize this candidate profile', explain_match: 'Explain this saved match', generate_job_description: 'Create a job description for a senior .NET developer' }
  return <><Header session={session} onLogout={onLogout} /><Container maxWidth="md" className="workspace-content"><Typography className="eyebrow">RECRUITMENT COPILOT</Typography><Typography variant="h2">AI assistant</Typography><Typography color="text.secondary" sx={{ mb: 3 }}>Ask supported questions using authorized HireFlow data. Responses use structured, validated operations and work without an external AI provider.</Typography>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    <Paper className="login-card" elevation={0}><Box component="form" onSubmit={submit} className="form-stack"><TextField label="Assistant task" select value={task} onChange={e => { setTask(e.target.value); setQuestion(questionDefaults[e.target.value]) }}><MenuItem value="find_candidates">Find candidates</MenuItem><MenuItem value="summarize_candidate">Summarize candidate</MenuItem><MenuItem value="explain_match">Explain saved match</MenuItem><MenuItem value="generate_job_description">Draft job description</MenuItem></TextField><TextField label="Question" value={question} onChange={e => setQuestion(e.target.value)} required multiline minRows={2} slotProps={{ htmlInput: { maxLength: 1000 } }} />{task !== 'find_candidates' && <TextField name="candidateId" label="Candidate ID (UUID)" helperText={task === 'explain_match' ? 'Required for saved match explanation' : 'Required for candidate summary'} required />}{task === 'explain_match' && <TextField name="jobId" label="Job ID (UUID)" required />}{task === 'generate_job_description' && <><TextField name="jobTitle" label="Job title" required /><TextField name="skills" label="Skills" helperText="Comma separated" /></>}<Button variant="contained" type="submit" disabled={busy}>{busy ? 'Working…' : 'Ask assistant'}</Button></Box></Paper>
    {result !== null && <Paper className="empty-state" sx={{ mt: 2, textAlign: 'left', overflow: 'auto' }}><Typography variant="h6" sx={{ mb: 1 }}>Assistant response</Typography><pre style={{ whiteSpace: 'pre-wrap', margin: 0, fontFamily: 'inherit' }}>{JSON.stringify(formatAssistantResult(result), null, 2)}</pre></Paper>}<footer className="app-footer">HireFlow AI <span>·</span> Recruitment, thoughtfully connected.</footer></Container></>
}

type AnalyticsData = { kpis: Record<string, number>; applicationsByJob: { job: string; applications: number }[]; recruitmentFunnel: { stage: string; count: number }[]; monthlyHiringTrends: { month: string; applications: number; hired: number }[]; recruiterPerformance: { recruiter: string; jobsAssigned: number; applications: number }[] }

function AnalyticsWorkspace({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [data, setData] = useState<AnalyticsData | null>(null); const [error, setError] = useState('')
  const headers = useMemo(() => ({ Authorization: `Bearer ${session.accessToken}` }), [session.accessToken])
  useEffect(() => { api.get<AnalyticsData>('/api/analytics/dashboard', { headers }).then(r => setData(r.data)).catch(() => setError('Could not load recruitment analytics. Check analytics permissions.')) }, [headers])
  const exportCsv = async () => { try { const response = await api.get('/api/analytics/applications.csv', { headers, responseType: 'blob' }); const url = URL.createObjectURL(response.data); const link = document.createElement('a'); link.href = url; link.download = 'hireflow-applications.csv'; link.click(); URL.revokeObjectURL(url) } catch { setError('Could not export application data.') } }
  const labels: [string, keyof AnalyticsData['kpis']][] = [['Candidates', 'totalCandidates'], ['Applications', 'totalApplications'], ['Open jobs', 'openJobs'], ['Shortlisted', 'shortlisted'], ['Interviewed', 'interviewed'], ['Hired', 'hired'], ['Rejected', 'rejected'], ['Conversion rate (%)', 'hiringConversionRate'], ['Time to hire (days)', 'averageTimeToHireDays']]
  return <><Header session={session} onLogout={onLogout} /><Container maxWidth="xl" className="workspace-content"><Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'end', mb: 3 }}><Box><Typography className="eyebrow">HIRING INSIGHTS</Typography><Typography variant="h2">Analytics</Typography><Typography color="text.secondary">Organization-level recruitment performance.</Typography></Box><Button variant="outlined" onClick={() => void exportCsv()}>Export applications</Button></Stack>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}{!data && !error && <CircularProgress />}
    {data && <><Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(150px,1fr))', gap: 1.5, mb: 3 }}>{labels.map(([label, key]) => <Card key={key} elevation={0} className="job-card"><CardContent><Typography variant="caption" color="text.secondary">{label}</Typography><Typography variant="h4" sx={{ mt: 1, fontWeight: 700 }}>{data.kpis[key]}{key === 'hiringConversionRate' ? '%' : ''}</Typography></CardContent></Card>)}</Box>
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}><Paper className="empty-state" sx={{ height: 340 }}><Typography variant="h6" sx={{ mb: 2 }}>Monthly applications and hires</Typography><ResponsiveContainer width="100%" height="85%"><LineChart data={data.monthlyHiringTrends}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="month" /><YAxis allowDecimals={false} /><Tooltip /><Line type="monotone" dataKey="applications" stroke="#17735e" strokeWidth={3} /><Line type="monotone" dataKey="hired" stroke="#d17a42" strokeWidth={3} /></LineChart></ResponsiveContainer></Paper>
    <Paper className="empty-state" sx={{ height: 340 }}><Typography variant="h6" sx={{ mb: 2 }}>Recruitment funnel</Typography><ResponsiveContainer width="100%" height="85%"><BarChart data={data.recruitmentFunnel}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="stage" /><YAxis allowDecimals={false} /><Tooltip /><Bar dataKey="count" fill="#17735e" radius={[7, 7, 0, 0]} /></BarChart></ResponsiveContainer></Paper></Box>
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2, mt: 2 }}><Paper className="empty-state" sx={{ textAlign: 'left' }}><Typography variant="h6" sx={{ mb: 1 }}>Applications by job</Typography>{data.applicationsByJob.map(row => <Stack key={row.job} direction="row" sx={{ justifyContent: 'space-between', borderTop: '1px solid #e6ece8', py: 1 }}><Typography>{row.job}</Typography><Typography sx={{ fontWeight: 700 }}>{row.applications}</Typography></Stack>)}</Paper><Paper className="empty-state" sx={{ textAlign: 'left' }}><Typography variant="h6" sx={{ mb: 1 }}>Recruiter performance</Typography>{data.recruiterPerformance.map(row => <Stack key={row.recruiter} direction="row" sx={{ justifyContent: 'space-between', borderTop: '1px solid #e6ece8', py: 1, gap: 2 }}><Typography>{row.recruiter} · {row.jobsAssigned} jobs</Typography><Typography sx={{ fontWeight: 700 }}>{row.applications} applications</Typography></Stack>)}</Paper></Box></>}
    <footer className="app-footer">HireFlow AI <span>·</span> Recruitment, thoughtfully connected.</footer></Container></>
}

type PipelineStage = { id: string; name: string; sortOrder: number; isTerminal: boolean }
type PipelineApplication = { id: string; candidateId: string; candidateName: string; email?: string; jobId: string; jobTitle: string; source: number; appliedAt: string; currentStageId: string; stage: string }

function PipelineWorkspace({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [stages, setStages] = useState<PipelineStage[]>([]); const [applications, setApplications] = useState<PipelineApplication[]>([]); const [error, setError] = useState('')
  const headers = useMemo(() => ({ Authorization: `Bearer ${session.accessToken}` }), [session.accessToken])
  const load = useCallback(async () => { try { const { data } = await api.get('/api/applications', { headers, params: { page: 1, pageSize: 100 } }); setStages(data.stages); setApplications(data.items); setError('') } catch { setError('Could not load the recruitment pipeline. Check application permissions.') } }, [headers])
  useEffect(() => { void load() }, [load])
  const move = async (applicationId: string, stageId: string) => { try { await api.put(`/api/applications/${applicationId}/stage`, { stageId }, { headers }); await load() } catch (e) { setError(axios.isAxiosError(e) ? e.response?.data?.detail ?? 'That stage transition is not allowed.' : 'Could not move the application.') } }
  return <><Header session={session} onLogout={onLogout} /><Container maxWidth="xl" className="workspace-content"><Box className="workspace-heading"><Typography className="eyebrow">RECRUITMENT WORKFLOW</Typography><Typography variant="h2">Candidate pipeline</Typography><Typography color="text.secondary">Move applications through stages and keep a complete history.</Typography></Box>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    <Box sx={{ display: 'grid', gridTemplateColumns: `repeat(${Math.max(stages.length, 1)}, minmax(220px, 1fr))`, gap: 2, overflowX: 'auto', pb: 2 }}>{stages.map(stage => <Paper key={stage.id} elevation={0} sx={{ p: 1.5, minHeight: 300, bgcolor: '#f2f6f3', borderRadius: 3 }}><Stack direction="row" sx={{ justifyContent: 'space-between', mb: 1.5 }}><Typography sx={{ fontWeight: 700 }}>{stage.name}</Typography><Chip size="small" label={applications.filter(x => x.currentStageId === stage.id).length} /></Stack>
      <Stack spacing={1.2}>{applications.filter(x => x.currentStageId === stage.id).map(application => <Card key={application.id} elevation={0} sx={{ border: '1px solid #e5ebe7', borderRadius: 2 }}><CardContent sx={{ '&:last-child': { pb: 2 } }}><Typography sx={{ fontWeight: 700 }}>{application.candidateName.trim() || application.email || 'Candidate'}</Typography><Typography variant="body2" color="text.secondary">{application.jobTitle}</Typography><Typography variant="caption" color="text.secondary">Applied {new Date(application.appliedAt).toLocaleDateString()}</Typography><TextField size="small" select fullWidth label="Move to stage" value={stage.id} sx={{ mt: 1.5 }} onChange={e => void move(application.id, e.target.value)}>{stages.map(option => <MenuItem key={option.id} value={option.id} disabled={option.id === stage.id}>{option.name}</MenuItem>)}</TextField></CardContent></Card>)}</Stack>
    </Paper>)}</Box><footer className="app-footer">HireFlow AI <span>·</span> Recruitment, thoughtfully connected.</footer></Container></>
}

function CandidateWorkspace({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [items, setItems] = useState<Candidate[]>([]); const [search, setSearch] = useState(''); const [open, setOpen] = useState(false)
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  const [parseResult, setParseResult] = useState<ParsedResumeSummary | null>(null)
  const [parseStatus, setParseStatus] = useState('')
  const headers = useMemo(() => ({ Authorization: `Bearer ${session.accessToken}` }), [session.accessToken])
  const load = useCallback(async () => {
    try { const { data } = await api.get<Page<Candidate>>('/api/candidates', { headers, params: { search, pageSize: 50 } }); setItems(data.items); setError('') }
    catch { setError('Could not load candidate records. Check your candidate permissions.') }
  }, [headers, search])
  useEffect(() => { void load() }, [load])
  const parseResume = async (candidate: Candidate) => {
    if (!candidate.primaryResumeId) return
    setError(''); setParseStatus('Parsing resume…'); setParseResult(null)
    try {
      await api.post(`/api/candidates/${candidate.id}/resumes/${candidate.primaryResumeId}/parse`, {}, { headers })
      setParseStatus('Resume parsing queued…')
      for (let attempt = 0; attempt < 30; attempt++) {
        await new Promise(resolve => setTimeout(resolve, 1000))
        const { data } = await api.get(`/api/candidates/${candidate.id}/resumes/${candidate.primaryResumeId}/parsing`, { headers })
        if (data.parsedData) setParseResult(data.parsedData)
        if (data.status === 'Completed' || data.status === 2) { setParseStatus('Resume parsing complete.'); await load(); return }
        if (data.status === 'Failed' || data.status === 3) { setParseStatus(data.errorMessage ?? 'Resume parsing failed.'); return }
      }
      setParseStatus('Resume parsing is still running. Check back shortly.')
    } catch { setError('Resume parsing could not be queued or checked. Please retry.') ; setParseStatus('') }
  }
  const create = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setBusy(true); setError('')
    const form = new FormData(event.currentTarget); const skillNames = String(form.get('skills') ?? '').split(',').map(x => x.trim()).filter(Boolean)
    try {
      const { data: candidate } = await api.post<Candidate>('/api/candidates', { firstName: form.get('firstName') || null, lastName: form.get('lastName') || null, email: form.get('email') || null, phoneNumber: form.get('phoneNumber') || null, location: form.get('location') || null, professionalSummary: form.get('professionalSummary') || null, totalExperienceYears: form.get('totalExperienceYears') ? Number(form.get('totalExperienceYears')) : null, skills: skillNames.map(name => ({ name, yearsExperience: null })), education: [], experience: [] }, { headers })
      const file = form.get('resume')
      if (file instanceof File && file.size > 0) { const body = new FormData(); body.append('file', file); await api.post(`/api/candidates/${candidate.id}/resumes`, body, { headers: { ...headers, 'Content-Type': 'multipart/form-data' } }) }
      setOpen(false); await load()
    } catch (e) { setError(axios.isAxiosError(e) ? e.response?.data?.message ?? e.response?.data?.detail ?? 'Could not save candidate. Check for a duplicate email or invalid resume.' : 'Could not save candidate.') }
    finally { setBusy(false) }
  }
  return <><Header session={session} onLogout={onLogout} /><Container maxWidth="lg" className="workspace-content">
    <Stack direction={{ xs: 'column', sm: 'row' }} className="workspace-heading" sx={{ justifyContent: 'space-between', alignItems: { sm: 'end' }, gap: 2 }}><div><Typography className="eyebrow">TALENT DATABASE</Typography><Typography variant="h2">Candidates</Typography><Typography color="text.secondary">Profiles, skills, and private resume files.</Typography></div><Stack direction="row" sx={{ gap: 1 }}><TextField size="small" label="Search candidates" value={search} onChange={e => setSearch(e.target.value)} /><Button variant="contained" onClick={() => setOpen(true)}>Add candidate</Button></Stack></Stack>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {parseStatus && <Alert severity="info" sx={{ mb: 2 }}>{parseStatus}</Alert>}
    {parseResult && <Paper className="empty-state" sx={{ mb: 2, textAlign: 'left' }}><Typography variant="h6">Latest resume extraction</Typography><Typography color="text.secondary">{[parseResult.name, parseResult.email, parseResult.phone, parseResult.total_experience != null ? `${formatExperience(parseResult.total_experience)} experience` : null].filter(Boolean).join(' · ')}</Typography><Typography sx={{ mt: 1 }}>Skills: {parseResult.skills?.join(', ') || 'None confidently identified'}</Typography>{parseResult.experience?.length ? <Box sx={{ mt: 1 }}><Typography sx={{ fontWeight: 650 }}>Work experience</Typography>{parseResult.experience.map((entry, index) => <Typography key={`${entry.company}-${entry.job_title}-${index}`} variant="body2">{[entry.job_title, entry.company, entry.date_range].filter(Boolean).join(' · ')}</Typography>)}</Box> : null}{parseResult.education?.length ? <Box sx={{ mt: 1 }}><Typography sx={{ fontWeight: 650 }}>Education</Typography>{parseResult.education.map((entry, index) => <Typography key={`${entry.institution}-${index}`} variant="body2">{[entry.degree, entry.field_of_study, entry.institution].filter(Boolean).join(' · ')}</Typography>)}</Box> : null}{parseResult.projects?.length ? <Box sx={{ mt: 1 }}><Typography sx={{ fontWeight: 650 }}>Projects</Typography>{parseResult.projects.map((project, index) => <Box key={`${project.name}-${index}`}><Typography variant="body2" sx={{ fontWeight: 600 }}>{[project.name, project.date_range].filter(Boolean).join(' · ')}</Typography>{project.description && <Typography variant="body2" color="text.secondary">{project.description.length > 600 ? `${project.description.slice(0, 600)}…` : project.description}</Typography>}</Box>)}</Box> : null}{parseResult.warnings?.length ? <Typography variant="caption" color="text.secondary">Notes: {parseResult.warnings.join(', ')}</Typography> : null}</Paper>}
    <Box className="job-grid">{items.map(candidate => <Card className="job-card" elevation={0} key={candidate.id}><CardContent>
      <Typography variant="h5" className="job-title">{[candidate.firstName, candidate.lastName].filter(Boolean).join(' ') || 'Unnamed candidate'}</Typography>
      <Typography color="text.secondary">{candidate.email || 'No email'}{candidate.location ? ` · ${candidate.location}` : ''}</Typography>
      <Typography className="job-description">{candidate.totalExperienceYears != null ? formatExperience(candidate.totalExperienceYears) : 'Not specified'} experience{candidate.primaryResumeId ? ' · Resume uploaded' : ''}</Typography>
      {candidate.primaryResumeId && <Button size="small" onClick={() => void parseResume(candidate)}>Parse resume</Button>}
    </CardContent></Card>)}</Box>
    {items.length === 0 && !error && <Paper className="empty-state"><Typography variant="h6">No candidates found</Typography><Typography color="text.secondary">Add a profile or change your search.</Typography></Paper>}
    <Dialog open={open} onClose={() => setOpen(false)} fullWidth maxWidth="sm"><Box component="form" onSubmit={create}><DialogTitle>Add candidate</DialogTitle><DialogContent className="form-stack">
      <Stack direction="row" sx={{ gap: 2 }}><TextField name="firstName" label="First name" fullWidth /><TextField name="lastName" label="Last name" fullWidth /></Stack>
      <TextField name="email" label="Email" type="email" /><TextField name="phoneNumber" label="Phone" /><TextField name="location" label="Location" />
      <TextField name="totalExperienceYears" label="Total experience (years)" type="number" slotProps={{ htmlInput: { min: 0, max: 80, step: 0.5 } }} />
      <TextField name="skills" label="Skills" helperText="Separate skills with commas" /><TextField name="professionalSummary" label="Professional summary" multiline minRows={3} />
      <Button component="label" variant="outlined">Choose PDF or DOCX resume<input name="resume" type="file" accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document" hidden /></Button>
    </DialogContent><DialogActions><Button onClick={() => setOpen(false)}>Cancel</Button><Button type="submit" variant="contained" disabled={busy}>{busy ? 'Saving…' : 'Save candidate'}</Button></DialogActions></Box></Dialog>
    <footer className="app-footer">HireFlow AI <span>·</span> Recruitment, thoughtfully connected.</footer>
  </Container></>
}

type Candidate = { id: string; firstName?: string; lastName?: string; email?: string; location?: string; totalExperienceYears?: number; primaryResumeId?: string }
type MatchingResult = { score: number; algorithmVersion: string; matchedSkills: string[]; missingRequiredSkills: string[]; categories: { label: string; weight: number; score: number; explanation: string }[]; warnings: string[] }
type ParsedResumeSummary = { status?: string; name?: string; email?: string; phone?: string; total_experience?: number; skills?: string[]; education?: { institution?: string; degree?: string; field_of_study?: string }[]; experience?: { company?: string; job_title?: string; date_range?: string }[]; projects?: { name?: string; date_range?: string; description?: string }[]; warnings?: string[] }
export default App









