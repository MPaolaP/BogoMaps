import { before, beforeEach, after, test } from 'node:test';
import { readFile } from 'node:fs/promises';
import { initializeTestEnvironment, assertSucceeds, assertFails } from '@firebase/rules-unit-testing';
import { doc, collection, setDoc, getDoc, getDocs, updateDoc, deleteDoc, serverTimestamp, Timestamp, query, where, orderBy, limit, documentId } from 'firebase/firestore';

let env;
const id = '0123456789abcdef0123456789abcdef';
const report = () => ({ ownerId: 'alice', city: 'bogota', category: 'Aceras', description: 'Andén deteriorado', address: 'Calle 53',
  latitude: 4.64, longitude: -74.06, severity: 'Moderado', anonymous: false, resolved: false,
  lighting: 'Sin evaluar', pavement: 'Deteriorado', accessibility: 'Con barreras', noise: 'Medio',
  createdAt: serverTimestamp(), updatedAt: serverTimestamp() });
const db = (uid, claims) => env.authenticatedContext(uid, claims).firestore();
const path = `bogoReports/${id}`;
const marker = () => ({ reportId: id, createdAt: serverTimestamp() });
before(async () => {
  env = await initializeTestEnvironment({ projectId: 'demo-bogomaps', firestore: { host: '127.0.0.1', port: 8080, rules: await readFile(new URL('../firestore.rules', import.meta.url), 'utf8') } });
});
beforeEach(async () => { await env.clearFirestore(); });
after(async () => { await env?.cleanup(); });

test('signed-in author creates a valid report and another account can read it', async () => {
  await assertSucceeds(setDoc(doc(db('alice'), path), report()));
  await assertSucceeds(getDoc(doc(db('bob'), path)));
});
test('guest cannot read or write shared reports', async () => {
  const guest = env.unauthenticatedContext().firestore();
  await assertFails(setDoc(doc(guest, path), report()));
  await assertFails(getDoc(doc(guest, path)));
});
test('reject forged ownership, demo identifiers, private fields and client timestamps', async () => {
  const alice = db('alice');
  await assertFails(setDoc(doc(alice, path), { ...report(), ownerId: 'bob' }));
  await assertFails(setDoc(doc(alice, 'bogoReports/sample-v1-1'), report()));
  await assertFails(setDoc(doc(alice, path), { ...report(), photo: '/private/photo.jpg' }));
  await assertFails(setDoc(doc(alice, path), { ...report(), createdAt: Timestamp.fromMillis(0) }));
  await assertFails(setDoc(doc(alice, path), { ...report(), latitude: 50 }));
  await assertFails(setDoc(doc(alice, path), { ...report(), lighting: 'Inventado' }));
});
test('queries must be bounded to at most fifty reports', async () => {
  const alice = db('alice');
  await setDoc(doc(alice, path), report());
  await assertSucceeds(getDocs(query(collection(alice, 'bogoReports'), where('city', '==', 'bogota'), orderBy('createdAt', 'desc'), orderBy(documentId(), 'desc'), limit(50))));
  await assertFails(getDocs(collection(alice, 'bogoReports')));
  await assertFails(getDocs(query(collection(alice, 'bogoReports'), limit(51))));
});
test('only author or server-appointed moderator can resolve; other fields are immutable', async () => {
  await setDoc(doc(db('alice'), path), report());
  await assertFails(updateDoc(doc(db('bob'), path), { resolved: true, updatedAt: serverTimestamp() }));
  await assertSucceeds(updateDoc(doc(db('alice'), path), { resolved: true, updatedAt: serverTimestamp() }));
  await assertSucceeds(updateDoc(doc(db('moderator', { moderator: true }), path), { resolved: false, updatedAt: serverTimestamp() }));
  await assertFails(updateDoc(doc(db('alice'), path), { ownerId: 'bob', updatedAt: serverTimestamp() }));
  await assertFails(updateDoc(doc(db('alice'), path), { description: 'Otro texto', updatedAt: serverTimestamp() }));
});
test('favorites are private and require an existing report', async () => {
  await setDoc(doc(db('alice'), path), report());
  const favorite = `bogoUsers/alice/favorites/${id}`;
  await assertSucceeds(setDoc(doc(db('alice'), favorite), marker()));
  await assertSucceeds(getDoc(doc(db('alice'), favorite)));
  await assertFails(getDoc(doc(db('bob'), favorite)));
  await assertFails(setDoc(doc(db('bob'), favorite), marker()));
  await assertSucceeds(deleteDoc(doc(db('alice'), favorite)));
  await assertFails(setDoc(doc(db('alice'), 'bogoUsers/alice/favorites/ffffffffffffffffffffffffffffffff'), { reportId: 'ffffffffffffffffffffffffffffffff', createdAt: serverTimestamp() }));
});
test('one immutable confirmation per user and report; no client-written counters or roles', async () => {
  await setDoc(doc(db('alice'), path), report());
  const confirmation = `bogoUsers/alice/confirmations/${id}`;
  await assertSucceeds(setDoc(doc(db('alice'), confirmation), marker()));
  await assertFails(setDoc(doc(db('alice'), confirmation), marker()));
  await assertFails(deleteDoc(doc(db('alice'), confirmation)));
  await assertFails(setDoc(doc(db('bob'), confirmation), marker()));
  await assertFails(updateDoc(doc(db('alice'), path), { confirmations: 9999 }));
  await assertFails(setDoc(doc(db('alice'), 'bogoUsers/alice'), { moderator: true }));
});
